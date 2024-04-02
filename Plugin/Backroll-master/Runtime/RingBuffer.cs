using System.Diagnostics;
using UnityEngine.Assertions;

namespace HouraiTeahouse.Backroll
{

	public class RingBuffer<T>
	{

		readonly T[] _data;

		public int _size;
		public int Capacity => _data.Length;
		public bool IsEmpty => _size == 0;

		int _head, _tail;

		public RingBuffer(int size)
		{
			_data = new T[size];
			_head = _tail = 0;
		}

		public ref T Peek()
		{
			Debug.Assert(_size != _data.Length);
			return ref _data[_tail];
		}

		public ref T this[int idx] => ref _data[(_tail + idx) % _data.Length];

		public void Pop()
		{
			Debug.Assert(_size != Capacity);
			_tail = (_tail + 1) % Capacity;
			_size--;
		}

		public void Push(in T val)
		{
			Debug.Assert(_size != (_data.Length - 1));
			_data[_head] = val;
			_head = (_head + 1) % Capacity;
			_size++;
		}

	}

}
