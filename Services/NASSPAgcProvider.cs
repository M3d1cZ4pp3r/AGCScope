using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace AGCScope.Services
{
    public class NASSPAgcProvider : IAgcProvider
    {
        [Flags]
        enum ProcessAccessFlags : uint
        {
            VmOperation = 0x8,
            VmRead = 0x10,
            VmWrite = 0x20,
            QueryInformation = 0x400,
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MemoryBasicInformation
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint AllocationProtect;
            public nuint RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
        }

        const uint MemCommit = 0x1000;
        const uint MemPrivate = 0x20000;

        const uint PageReadWrite = 0x04;
        const uint PageWriteCopy = 0x08;
        const uint PageExecuteReadWrite = 0x40;
        const uint PageExecuteWriteCopy = 0x80;

        const uint PageGuard = 0x100;
        const uint PageNoAccess = 0x01;

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(ProcessAccessFlags flags, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll")]
        static extern nuint VirtualQueryEx(IntPtr hProcess, IntPtr address, out MemoryBasicInformation mbi, nuint length);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr baseAddress, byte[] buffer, nuint size, out nuint bytesRead);

        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr handle);


        private const string OrbiterProcess = "Orbiter";

        // In AGC Words
        private const int NumBanks = 8;
        private const int ErasableBankWords = 256;
        private const int ErasableWords = NumBanks * ErasableBankWords;
        
        // In bytes
        private const int ErasableBankSize = 2 * ErasableBankWords;
        private const int ErasableSize = 2 * ErasableWords;


        private RopeImage coreRopeImage = null!;

        private IntPtr processHandle = IntPtr.Zero;

        private nuint erasableBaseAddress;

        private byte[] erasableImage;

        public NASSPAgcProvider()
        {
            erasableImage = new byte[ErasableSize];
        }

        public void Connect(RopeImage ropeImage)
        {
            if(processHandle != IntPtr.Zero)
            {
                CloseHandle(processHandle);
                processHandle = IntPtr.Zero;
            }


            // Find Orbiter/NASSP process
            if(Process.GetProcessesByName(OrbiterProcess).Length <= 0)
            {
                MessageBox.Show("NASSP needs to be running to connect to the AGC");
                return;
            }

            coreRopeImage = ropeImage;

            int processId = Process.GetProcessesByName(OrbiterProcess)[0].Id;
            processHandle = OpenProcess(ProcessAccessFlags.VmOperation | ProcessAccessFlags.VmRead | ProcessAccessFlags.QueryInformation, false, processId);

            if (processHandle == IntPtr.Zero)
                throw new InvalidOperationException($"OpenProcess failed: {Marshal.GetLastWin32Error()}");

            nuint? erasableAddress = FindErasableAddress();

            if(!erasableAddress.HasValue)
            {
                MessageBox.Show("Can't find AGC instance with identical Core Rope Memory as provided");
                CloseHandle(processHandle);
                processHandle = IntPtr.Zero;
                return;
            }

            erasableBaseAddress = erasableAddress.Value;

            RefreshMemory();
        }

        ~NASSPAgcProvider()
        {
            if(processHandle != IntPtr.Zero)
            {
                CloseHandle(processHandle);
                processHandle = IntPtr.Zero;
            }
        }

        private nuint? FindErasableAddress()
        {
            // Search for memory pattern

            nuint address = 0;

            while (true)
            {
                // Query memory information
                if (VirtualQueryEx(processHandle, (IntPtr)address, out var mbi, (nuint)Marshal.SizeOf<MemoryBasicInformation>()) == 0)
                    break;

                uint protection = mbi.Protect & 0xFF;
                bool writable = protection == PageReadWrite ||
                                protection == PageWriteCopy ||
                                protection == PageExecuteReadWrite ||
                                protection == PageExecuteWriteCopy;

                // AGC lies on the Heap
                bool scan = mbi.State == MemCommit && writable && mbi.Type == MemPrivate;

                if (scan)
                {
                    var buffer = new byte[(int)mbi.RegionSize];

                    if (ReadProcessMemory(processHandle, mbi.BaseAddress, buffer, (nuint)buffer.Length, out var bytesRead))
                    {
                        int length = checked((int)bytesRead);

                        foreach (int offset in FindPattern(buffer, length, coreRopeImage.GetBlock(0, 64).ToArray()))
                        {
                            // Check for full equality
                            ReadOnlySpan<byte> expected = coreRopeImage.GetAsBytes();
                            ReadOnlySpan<byte> actual = buffer.AsSpan(offset, expected.Length);

                            bool equal = expected.SequenceEqual(actual);

                            if (equal)
                            {
                                // we found the AGCs fixed core rope
                                // Now calculate where the Erasable is
                                // we just need to rewind the Erasable block since Fixed comes directly after
                                nuint erasableStart = (nuint)mbi.BaseAddress + (nuint)offset - (ErasableSize);
                                return erasableStart;
                            }
                        }
                    }
                }

                // Continue at next block
                nuint next = (nuint)mbi.BaseAddress + mbi.RegionSize;

                if (next <= address)
                    break;

                address = next;
            }

            return null;
        }

        private static IEnumerable<int> FindPattern(byte[] data, int length, ushort[] pattern)
        {
            // Compare two bytes at once
            for(int i = 0; i <= length - pattern.Length * 2; i++)
            {
                bool match = true;

                for (int j = 0; j < pattern.Length; j++)
                {
                    // Convert to high or low byte
                    byte low = (byte)(pattern[j] & 0x00FF);
                    byte high = (byte)(pattern[j] >> 8);
                    // Little endian compare
                    if (data[i + j * 2] != low && data[i + (j * 2 + 1)] != high)
                    {
                        match = false;
                        break;
                    }
                }

                if (match) yield return i;
            }
        }

        public void RefreshMemory()
        {
            // Processor handle is indicator for a valid base address
            if (processHandle == IntPtr.Zero)
                return;

            // Refreshing the whole block is the concept right now
            bool success = ReadProcessMemory(processHandle, (nint)erasableBaseAddress, erasableImage, ErasableSize, out var bytesRead);
            if(!success || bytesRead == 0)
            {
                CloseHandle(processHandle);
                processHandle = IntPtr.Zero;   
            }
        }

        public ushort Read(uint bank, uint address)
        {
            if (address > ErasableBankWords || bank >= NumBanks) throw new ArgumentOutOfRangeException("Tried to access erasable memory that doesn't exist");
            uint bankStart = bank * ErasableBankSize;
            return MemoryMarshal.Cast<byte, ushort>(MemoryMarshal.CreateSpan(ref erasableImage[bankStart], ErasableBankSize))[(int)address];
        }

        public bool IsConnected()
        {
            return processHandle != IntPtr.Zero;
        }
    }
}
