using System;
using System.Collections.Generic;
using UnityEngine;

namespace Links.Licenses
{
	struct UnityLogScope : IDisposable
	{
		struct LogLevel
		{
			public LogType LogType;
			public StackTraceLogType StackTraceType;

			public LogLevel(LogType logType, StackTraceLogType stackTraceType)
			{
				LogType = logType;
				StackTraceType = stackTraceType;
			}
		}

		static LogLevel[] m_None = new LogLevel[]
		{
			new LogLevel(LogType.Log, StackTraceLogType.None),
			new LogLevel(LogType.Warning, StackTraceLogType.None),
			new LogLevel(LogType.Error, StackTraceLogType.None),
			new LogLevel(LogType.Exception, StackTraceLogType.None),
			new LogLevel(LogType.Assert, StackTraceLogType.None),
		};

		List<LogLevel> m_Original;

		UnityLogScope(LogLevel[] stackTraceLevels)
		{
			m_Original = new List<LogLevel>(stackTraceLevels.Length);
			foreach (var level in stackTraceLevels)
			{
				var stackTraceType = Application.GetStackTraceLogType(level.LogType);
				m_Original.Add(new LogLevel(level.LogType, stackTraceType));

				Application.SetStackTraceLogType(level.LogType, level.StackTraceType);
			}
		}

		public static UnityLogScope CreateNone()
			=> new UnityLogScope(m_None);

		public void Dispose()
		{
			foreach(var level in m_Original)
			{
				Application.SetStackTraceLogType(level.LogType, level.StackTraceType);
			}
		}
	}
}
