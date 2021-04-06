﻿
namespace GameFramework.Singleton
{
	public class Singleton<T> where T : class, new()
	{
		private static T m_instance;
		private static object m_lock = new object();

		public static T Instance
		{
			get
			{
				lock (m_lock)
				{
					if (m_instance == null)
					{
						m_instance = new T();
						Log.Debug("[Singleton] - {0} has setup.", typeof(T).Name);
					}
				}

				return m_instance;
			}

		}
	}
}
