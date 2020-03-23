/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2009-2015 Michael Möller <mmoeller@openhardwaremonitor.org>
  Copyright (C) 2010 Paul Werelds
  Copyright (C) 2011 Roland Reinl <roland-reinl@gmx.de>
  Modified by Michał Młodawski Simplemethod.io https://github.com/SimpleMethod 2020
	
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using OpenHardwareMonitor.Collections;

namespace OpenHardwareMonitor.Hardware.HDD
{
    internal abstract class AbstractHarddrive : Hardware
    {

        private const int UPDATE_DIVIDER = 30; // update only every 30s

        // array of all harddrive types, matching type is searched in this order
        private static Type[] hddTypes = {
      typeof(SSDPlextor),
      typeof(SSDIntel),
      typeof(SSDSandforce),
      typeof(SSDIndilinx),
      typeof(SSDSamsung),
      typeof(SSDMicron),
      typeof(GenericHarddisk)
    };

        private string firmwareRevision;
        private readonly ISmart smart;

        private readonly IntPtr handle;
        private readonly int index;
        private int count;

        private IList<SmartAttribute> smartAttributes;
        private IDictionary<SmartAttribute, Sensor> sensors;

        private DriveInfo[] driveInfos;
        private Sensor usageSensor;

        protected AbstractHarddrive(ISmart smart, string name,
          string firmwareRevision, int index,
          IEnumerable<SmartAttribute> smartAttributes, ISettings settings)
          : base(name, new Identifier("hdd",
            index.ToString(CultureInfo.InvariantCulture)), settings)
        {
            this.firmwareRevision = firmwareRevision;
            this.smart = smart;
            handle = smart.OpenDrive(index);

            if (handle != smart.InvalidHandle)
                smart.EnableSmart(handle, index);

            this.index = index;
            this.count = 0;

            this.smartAttributes = new List<SmartAttribute>(smartAttributes);

            string[] logicalDrives = smart.GetLogicalDrives(index);
            List<DriveInfo> driveInfoList = new List<DriveInfo>(logicalDrives.Length);
            foreach (string logicalDrive in logicalDrives)
            {
                try
                {
                    DriveInfo di = new DriveInfo(logicalDrive);
                    if (di.TotalSize > 0)
                        driveInfoList.Add(new DriveInfo(logicalDrive));
                }
                catch (ArgumentException)
                {
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
            driveInfos = driveInfoList.ToArray();

            CreateSensors();
        }

        public static AbstractHarddrive CreateInstance(ISmart smart,
          int driveIndex, ISettings settings)
        {
            IntPtr deviceHandle = smart.OpenDrive(driveIndex);

            string name = null;
            string firmwareRevision = null;
            DriveAttributeValue[] values = { };

            if (deviceHandle != smart.InvalidHandle)
            {
                bool nameValid = smart.ReadNameAndFirmwareRevision(deviceHandle,
                driveIndex, out name, out firmwareRevision);
                bool smartEnabled = smart.EnableSmart(deviceHandle, driveIndex);

                if (smartEnabled)
                    values = smart.ReadSmartData(deviceHandle, driveIndex);

                smart.CloseHandle(deviceHandle);

                if (!nameValid)
                {
                    name = null;
                    firmwareRevision = null;
                }
            }
            else
            {
                string[] logicalDrives = smart.GetLogicalDrives(driveIndex);
                if (logicalDrives == null || logicalDrives.Length == 0)
                    return null;

                bool hasNonZeroSizeDrive = false;
                foreach (string logicalDrive in logicalDrives)
                {
                    try
                    {
                        DriveInfo di = new DriveInfo(logicalDrive);
                        if (di.TotalSize > 0)
                        {
                            hasNonZeroSizeDrive = true;
                            break;
                        }
                    }
                    catch (ArgumentException)
                    {
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }

                if (!hasNonZeroSizeDrive)
                    return null;
            }

            if (string.IsNullOrEmpty(name))
                name = "Generic Hard Disk";

            if (string.IsNullOrEmpty(firmwareRevision))
                firmwareRevision = "Unknown";

            foreach (Type type in hddTypes)
            {
                // get the array of name prefixes for the current type
                NamePrefixAttribute[] namePrefixes = type.GetCustomAttributes(
                  typeof(NamePrefixAttribute), true) as NamePrefixAttribute[];

                // get the array of the required SMART attributes for the current type
                RequireSmartAttribute[] requiredAttributes = type.GetCustomAttributes(
                  typeof(RequireSmartAttribute), true) as RequireSmartAttribute[];

                // check if all required attributes are present
                bool allRequiredAttributesFound = true;
                foreach (var requireAttribute in requiredAttributes)
                {
                    bool adttributeFound = false;
                    foreach (DriveAttributeValue value in values)
                    {
                        if (value.Identifier == requireAttribute.AttributeId)
                        {
                            adttributeFound = true;
                            break;
                        }
                    }
                    if (!adttributeFound)
                    {
                        allRequiredAttributesFound = false;
                        break;
                    }
                }

                // if an attribute is missing, then try the next type
                if (!allRequiredAttributesFound)
                    continue;

                // check if there is a matching name prefix for this type
                foreach (NamePrefixAttribute prefix in namePrefixes)
                {
                    if (name.StartsWith(prefix.Prefix, StringComparison.InvariantCulture))
                        return Activator.CreateInstance(type, smart, name, firmwareRevision,
                          driveIndex, settings) as AbstractHarddrive;
                }
            }

            // no matching type has been found
            return null;
        }

        private void CreateSensors()
        {
            sensors = new Dictionary<SmartAttribute, Sensor>();

            if (handle != smart.InvalidHandle)
            {
                IList<Pair<SensorType, int>> sensorTypeAndChannels =
                  new List<Pair<SensorType, int>>();

                DriveAttributeValue[] values = smart.ReadSmartData(handle, index);

                foreach (SmartAttribute attribute in smartAttributes)
                {
                    if (!attribute.SensorType.HasValue)
                        continue;

                    bool found = false;
                    foreach (DriveAttributeValue value in values)
                    {
                        if (value.Identifier == attribute.Identifier)
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                        continue;

                    Pair<SensorType, int> pair = new Pair<SensorType, int>(
                      attribute.SensorType.Value, attribute.SensorChannel);

                    if (!sensorTypeAndChannels.Contains(pair))
                    {
                        Sensor sensor = new Sensor(attribute.SensorName,
                          attribute.SensorChannel, attribute.DefaultHiddenSensor,
                          attribute.SensorType.Value, this, attribute.ParameterDescriptions,
                          settings);

                        sensors.Add(attribute, sensor);
                        ActivateSensor(sensor);
                        sensorTypeAndChannels.Add(pair);
                    }
                }
            }

            if (driveInfos.Length > 0)
            {
                usageSensor =
                  new Sensor("Used Space", 0, SensorType.Load, this, settings);
                ActivateSensor(usageSensor);
            }
        }

        public override HardwareType HardwareType
        {
            get { return HardwareType.HDD; }
        }

        public virtual void UpdateAdditionalSensors(DriveAttributeValue[] values) { }

        public override void Update()
        {
            if (count == 0)
            {
                if (handle != smart.InvalidHandle)
                {
                    DriveAttributeValue[] values = smart.ReadSmartData(handle, index);

                    foreach (KeyValuePair<SmartAttribute, Sensor> keyValuePair in sensors)
                    {
                        SmartAttribute attribute = keyValuePair.Key;
                        foreach (DriveAttributeValue value in values)
                        {
                            if (value.Identifier == attribute.Identifier)
                            {
                                Sensor sensor = keyValuePair.Value;
                                sensor.Value = attribute.ConvertValue(value, sensor.Parameters);
                            }
                        }
                    }

                    UpdateAdditionalSensors(values);
                }

                if (usageSensor != null)
                {
                    long totalSize = 0;
                    long totalFreeSpace = 0;

                    for (int i = 0; i < driveInfos.Length; i++)
                    {
                        if (!driveInfos[i].IsReady)
                            continue;
                        try
                        {
                            totalSize += driveInfos[i].TotalSize;
                            totalFreeSpace += driveInfos[i].TotalFreeSpace;
                        }
                        catch (IOException) { }
                        catch (UnauthorizedAccessException) { }
                    }
                    if (totalSize > 0)
                    {
                        usageSensor.Value = 100.0f - (100.0f * totalFreeSpace) / totalSize;
                    }
                    else
                    {
                        usageSensor.Value = null;
                    }
                }
            }

            count++;
            count %= UPDATE_DIVIDER;
        }

        public override string GetReport()
        {
            Drive_Model driveModel = new Drive_Model();
            Queue<Drive> drives = new Queue<Drive>();
            
            foreach (DriveInfo di in driveInfos)
            {
                if (!di.IsReady)
                    continue;
                try
                {
                    drives.Enqueue(new Drive(di.TotalFreeSpace, di.TotalSize, di.DriveFormat, di.Name, firmwareRevision, name));
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }

            driveModel.Drive = drives;
            return JsonSerializer.Serialize(driveModel);
        }

        protected static float RawToInt(byte[] raw, byte value,
          IReadOnlyArray<IParameter> parameters)
        {
            return (raw[3] << 24) | (raw[2] << 16) | (raw[1] << 8) | raw[0];
        }

        public override void Close()
        {
            if (handle != smart.InvalidHandle)
                smart.CloseHandle(handle);

            base.Close();
        }

        public override void Traverse(IVisitor visitor)
        {
            foreach (ISensor sensor in Sensors)
                sensor.Accept(visitor);
        }
    }
}
