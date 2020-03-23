/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2012-2013 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/

using System;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace OpenHardwareMonitor.Hardware.RAM {
    public class MemoryRam
    {
        public double TotalPhysicalMemory { get; set; }
        public double TotalAvailableMemory { get; set; }

    }
    internal class RAMGroup : IGroup {

    private Hardware[] hardware;

    public RAMGroup(SMBIOS smbios, ISettings settings) {

      // No implementation for RAM on Unix systems
      int p = (int)Environment.OSVersion.Platform;
      if ((p == 4) || (p == 128)) {
        hardware = new Hardware[0];
        return;
      }

      hardware = new Hardware[] { new GenericRAM("Generic Memory", settings) };
    }
    [DllImport("kernel32.dll")]
    static extern bool GetPhysicallyInstalledSystemMemory(out long TotalMemoryInKilobytes);
        public string GetReport() {
        MemoryRam memoryRam = new MemoryRam();
        long phymemory;
        GetPhysicallyInstalledSystemMemory(out phymemory);
        var performance = new System.Diagnostics.PerformanceCounter("Memory", "Available KBytes");
        memoryRam.TotalPhysicalMemory = phymemory / 1024.00 / 1024.00;
        memoryRam.TotalAvailableMemory = performance.RawValue / 1024.00;
        return JsonSerializer.Serialize(memoryRam);
        }

    public IHardware[] Hardware {
      get {
        return hardware;
      }
    }

    public void Close() {
      foreach (Hardware ram in hardware)
        ram.Close();
    }
  }
}
