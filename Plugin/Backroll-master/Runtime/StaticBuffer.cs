using System.Diagnostics;
using UnityEngine.Assertions;

namespace HouraiTeahouse.Backroll
{

	public class StaticBuffer<T>
	{

		readonly T[] _elements;
		public int Size { get; private set; }
		public int Capacity => _elements.Length;

		public StaticBuffer(int size)
		{
			_elements = new T[size];
			Size = 0;
		}

		public ref T this[int idx]
		{
			get
			{
				Debug.Assert(idx >= 0 && idx < Size);
				return ref _elements[idx];
			}
		}

		public void Push(in T val)
		{
			Debug.Assert(Size != (_elements.Length - 1));
			_elements[Size++] = val;
		}

	}

}
