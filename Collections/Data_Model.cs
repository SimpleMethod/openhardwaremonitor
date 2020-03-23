/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2020 Michał Młodawski Simplemethod.io https://github.com/SimpleMethod 
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace OpenHardwareMonitor.Collections
{

    public class NVidiaGpu
    {
        public string Name { get; set; }
        public string DriverVersion { get; set; }
        public string DriverBranch { get; set; }
        public string DeviceID { get; set; }
        public string RevisionID { get; set; }
        public long CurrentTemp { get; set; }
        public long TotalMemory { get; set; }
        public long AvailableMemory {get; set; }
    }

    public class Drive
    {
   
        public string DriveName { get; set; }
        public string FirmwareVersion { get; set; }
        public string LogicalDriveName { get; set; }
        public string Format { get; set; }
        public long TotalSize { get; set; }
        public long TotalFreeSpace { get; set; }
    }

    public class Smbios
    {
        
        public string BiosVendor { get; set; }
        public string BiosVersion { get; set; }
        public string MainboardManufacturer { get; set; }
        public string MainboardName { get; set; }
        public string ProcessorManufacturer { get; set; }
        public string ProcessorVersion { get; set; }
        public string ProcessorCoreCount { get; set; }
        public string ProcessorThreadCount { get; set; }
        [JsonPropertyName("Memory")]
        public Queue<RamMemory> RamMemory { get; set; } 
    }

    public class RamMemory
    {
        public RamMemory(string memoryDeviceManufacturer, string memoryDevicePartNumber, string memoryDeviceLocator, string memoryDeviceBankLocator, string memorySpeed)
        {
            MemoryDeviceManufacturer = memoryDeviceManufacturer;
            MemoryDevicePartNumber = memoryDevicePartNumber;
            MemoryDeviceLocator = memoryDeviceLocator;
            MemoryDeviceBankLocator = memoryDeviceBankLocator;
            MemorySpeed = memorySpeed;
        }

        public string MemoryDeviceManufacturer { get; set; }
        public string MemoryDevicePartNumber { get; set; }
        public string MemoryDeviceLocator { get; set; }
        public string MemoryDeviceBankLocator { get; set; }
        public string MemorySpeed { get; set; }
    }
}
