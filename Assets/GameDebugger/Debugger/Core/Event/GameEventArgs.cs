//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2019 Jiang Yin. All rights reserved.
// Homepage: http://gameframework.cn/
// Feedback: mailto:jiangyin@gameframework.cn
//------------------------------------------------------------
using System;

namespace GameFramework.Event
{
	/// <summary>
	/// 游戏逻辑事件基类。
	/// </summary>
	public abstract class GameEventArgs : EventArgs, IReference
	{
		/// <summary>
		/// 获取类型编号。
		/// </summary>
		public abstract int Id
		{
			get;
		}

		/// <summary>
		/// 清理引用。
		/// </summary>
		public abstract void Clear();
	}
}
