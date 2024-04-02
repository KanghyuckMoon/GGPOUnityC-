using System;
using System.Text;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Assertions;

namespace HouraiTeahouse.Backroll
{

	public unsafe struct GameInput
	{

		public const int NullFrame = -1;
		public const int kMaxBytes = 9;

		public int frame;
		public uint size;
		public fixed byte bits[BackrollConstants.kMaxPlayers * kMaxBytes];

		public GameInput(int iframe, void* ibits, uint isize)
		{
			Assert.IsTrue(isize > 0);
			Assert.IsTrue(isize <= kMaxBytes * BackrollConstants.kMaxPlayers);
			frame = iframe;
			size = isize;
			fixed (byte* ptr = bits)
			{
				UnsafeUtility.MemClear(ptr, kMaxBytes * BackrollConstants.kMaxPlayers);
				if (ibits != null)
				{
					UnsafeUtility.MemCpy(ptr, ibits, isize);
				}
			}
		}

		public static GameInput Create<T>(int iframe, ref T value, uint offset = 0) where T : struct
		{
			var size = UnsafeUtility.SizeOf<T>();
			var input = new GameInput(iframe, null, (uint)size);
			UnsafeUtility.CopyStructureToPtr(ref value, input.bits + size * offset);
			return input;
		}

		public bool value(int i)
		{
			return (bits[i / 8] & (1 << (i % 8))) != 0;
		}
		public void set(int i)
		{
			fixed (byte* ptr = bits)
			{
				ptr[i / 8] |= (byte)(1 << (i % 8));
			}
		}
		public void clear(int i)
		{
			fixed (byte* ptr = bits)
			{
				ptr[i / 8] &= (byte)~(1 << (i % 8));
			}
		}
		public void erase()
		{
			fixed (byte* ptr = bits)
			{
				UnsafeUtility.MemClear(ptr, size);
			}
		}
		public string desc(bool show_frame = true)
		{
			Assert.IsTrue(size > 0);
			string retVal;
			if (show_frame)
			{
				retVal = $"(frame:{frame} size:{size} ";
			}
			else
			{
				retVal = $"(size:{size} ";
			}
			var builder = new StringBuilder(retVal);
			for (var i = 0; i < size; i++)
			{
				builder.AppendFormat("{0:x2}", bits[size]);
			}
			builder.Append(")");
			return builder.ToString();
		}
		public bool equal(in GameInput other, bool bitsonly)
		{
			if (!bitsonly && frame != other.frame)
			{
				Debug.Log($"frames don't match: {frame}, {other.frame}");
			}
			if (size != other.size)
			{
				Debug.Log($"sizes don't match: {size}, {other.size}");
			}
			fixed (byte* ptr = bits, otherPtr = other.bits)
			{
				if (UnsafeUtility.MemCmp(ptr, otherPtr, size) != 0)
				{
					Debug.Log("bits don't match");
				}
				Debug.Assert(size > 0 && other.size > 0);
				return (bitsonly || frame == other.frame) &&
						size == other.size &&
						UnsafeUtility.MemCmp(ptr, otherPtr, size) == 0;
			}
		}
	}


}
