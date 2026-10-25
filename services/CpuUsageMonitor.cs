using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MeoPet
{
    internal sealed class CpuUsageMonitor
    {
        private ulong _lastIdle;
        private ulong _lastKernel;
        private ulong _lastUser;
        private bool _hasPreviousSample;

        public bool TryGetUsage(out float percentage)
        {
            SystemFileTime idle;
            SystemFileTime kernel;
            SystemFileTime user;
            if (!GetSystemTimes(out idle, out kernel, out user))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            ulong currentIdle = idle.Value;
            ulong currentKernel = kernel.Value;
            ulong currentUser = user.Value;
            percentage = 0F;

            if (!_hasPreviousSample)
            {
                SaveSample(currentIdle, currentKernel, currentUser);
                return false;
            }

            if (currentIdle < _lastIdle || currentKernel < _lastKernel || currentUser < _lastUser)
            {
                SaveSample(currentIdle, currentKernel, currentUser);
                return false;
            }

            ulong idleDelta = currentIdle - _lastIdle;
            ulong totalDelta = (currentKernel - _lastKernel) + (currentUser - _lastUser);
            SaveSample(currentIdle, currentKernel, currentUser);

            if (totalDelta == 0)
            {
                return false;
            }

            percentage = (float)(100.0 * (1.0 - (double)idleDelta / totalDelta));
            percentage = Math.Max(0F, Math.Min(100F, percentage));
            return true;
        }

        private void SaveSample(ulong idle, ulong kernel, ulong user)
        {
            _lastIdle = idle;
            _lastKernel = kernel;
            _lastUser = user;
            _hasPreviousSample = true;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SystemFileTime
        {
            public uint LowDateTime;
            public uint HighDateTime;

            public ulong Value
            {
                get { return ((ulong)HighDateTime << 32) | LowDateTime; }
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(
            out SystemFileTime idleTime,
            out SystemFileTime kernelTime,
            out SystemFileTime userTime);
    }
}
