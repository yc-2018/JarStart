using System;
using System.Collections.Generic;

namespace JarStart
{
    /// <summary>
    /// 每个 jar 的日志只存在内存里：环形缓冲，超出上限就淘汰最早的行。
    /// 写入发生在子进程的输出线程里，因此所有操作都加锁。
    /// </summary>
    public class LogStore
    {
        private readonly object _sync = new object();
        private readonly Dictionary<string, List<LogLine>> _buffers = new Dictionary<string, List<LogLine>>();
        private int _max = 3000;

        /// <summary>新日志到达时触发（后台线程触发，界面侧需要自行切回 UI 线程）</summary>
        public event Action<string, LogLine> LineAppended;

        public LogStore(int maxLines)
        {
            SetMax(maxLines);
        }

        public int MaxLines
        {
            get { lock (_sync) return _max; }
        }

        public void SetMax(int maxLines)
        {
            if (maxLines < 200) maxLines = 200;
            lock (_sync)
            {
                _max = maxLines;
                foreach (var kv in _buffers) Trim(kv.Value);
            }
        }

        public void Append(string jarId, LogLevel level, string text)
        {
            var line = new LogLine(DateTime.Now, level, text);
            lock (_sync)
            {
                List<LogLine> buf;
                if (!_buffers.TryGetValue(jarId, out buf))
                {
                    buf = new List<LogLine>();
                    _buffers[jarId] = buf;
                }
                buf.Add(line);
                Trim(buf);
            }
            var h = LineAppended;
            if (h != null) h(jarId, line);
        }

        public void AppendSys(string jarId, string text)
        {
            Append(jarId, LogLevel.Sys, text);
        }

        /// <summary>取出某个 jar 的全部日志副本</summary>
        public List<LogLine> Get(string jarId)
        {
            lock (_sync)
            {
                List<LogLine> buf;
                if (!_buffers.TryGetValue(jarId, out buf)) return new List<LogLine>();
                return new List<LogLine>(buf);
            }
        }

        public void Clear(string jarId)
        {
            lock (_sync)
            {
                _buffers.Remove(jarId);
            }
        }

        public void ClearAll()
        {
            lock (_sync)
            {
                _buffers.Clear();
            }
        }

        private void Trim(List<LogLine> buf)
        {
            if (buf.Count <= _max) return;
            // 一次砍掉多余的一半，避免每写一行就 Shift 一次
            int drop = buf.Count - _max / 2;
            if (drop <= 0) drop = buf.Count - _max;
            if (drop > 0) buf.RemoveRange(0, drop);
        }
    }
}
