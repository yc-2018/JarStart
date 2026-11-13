using System;
using System.Runtime.InteropServices;

namespace JarStart
{
    /// <summary>
    /// 用 Windows 的 Job Object 托管子进程：
    /// 勾上「关闭启动器时停止所有子进程」后，子进程会被登记进这个 Job，
    /// 于是启动器无论是正常退出、崩溃还是被任务管理器强杀，Windows 都会把 Job 里的进程一起结束。
    /// 不登记的话，子进程就会脱离启动器继续在后台跑（变成孤儿进程）。
    /// </summary>
    public class JobGuard
    {
        private const int JobObjectExtendedLimitInformation = 9;
        private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetInformationJobObject(IntPtr hJob, int infoClass,
            ref JOBOBJECT_EXTENDED_LIMIT_INFORMATION info, uint length);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount;
            public ulong ReadTransferCount, WriteTransferCount, OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        private IntPtr _job = IntPtr.Zero;

        public bool Available { get { return _job != IntPtr.Zero; } }

        /// <summary>创建一个"关闭句柄即杀掉全部成员"的 Job</summary>
        public bool Create()
        {
            if (_job != IntPtr.Zero) return true;
            try
            {
                _job = CreateJobObject(IntPtr.Zero, null);
                if (_job == IntPtr.Zero) return false;

                var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
                info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
                int size = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
                if (!SetInformationJobObject(_job, JobObjectExtendedLimitInformation, ref info, (uint)size))
                {
                    CloseHandle(_job);
                    _job = IntPtr.Zero;
                    return false;
                }
                return true;
            }
            catch
            {
                _job = IntPtr.Zero;
                return false;
            }
        }

        /// <summary>把子进程登记进来（连带它自己再派生的子进程）</summary>
        public bool Assign(IntPtr processHandle)
        {
            if (_job == IntPtr.Zero || processHandle == IntPtr.Zero) return false;
            try { return AssignProcessToJobObject(_job, processHandle); }
            catch { return false; }
        }

        /// <summary>关闭 Job：里面登记的进程会立刻被结束</summary>
        public void Close()
        {
            if (_job == IntPtr.Zero) return;
            try { CloseHandle(_job); } catch { }
            _job = IntPtr.Zero;
        }
    }
}
