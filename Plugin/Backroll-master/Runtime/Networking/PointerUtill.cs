using HouraiTeahouse.Backroll;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public static class PointerUtill
{
	public static IntPtr ToIntPtr(object obj)
	{
		//Sync테스트할 때는 Pinned 원래는 Weak
		//GCHandle handle = GCHandle.Alloc(obj, GCHandleType.Weak);
		GCHandle handle = GCHandle.Alloc(obj, GCHandleType.Normal);
		//GCHandle handle = GCHandle.Alloc(obj, GCHandleType.Pinned);
		return GCHandle.ToIntPtr(handle);
	}

	public static IntPtr ToIntPtrStruct<T>(T obj) where T : struct
	{
		IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(T)));
		Marshal.StructureToPtr(obj, ptr, false);
		return ptr;
	}
	public static T[] IntPtrToArray<T>(IntPtr ptr, int length) where T : struct
	{
		T[] array = new T[length];

		int size = Marshal.SizeOf(typeof(T));

		for (int i = 0; i < length; i++)
		{
			IntPtr currentPtr = new IntPtr(ptr.ToInt64() + (i * size));
			array[i] = Marshal.PtrToStructure<T>(currentPtr);
		}

		return array;
	}

	public static IntPtr ToIntPtrStructArray<T>(T[] array, out IntPtr unmanagedArrayPointer) where T : struct
	{
		int size = Marshal.SizeOf(typeof(T)) * array.Length;

		unmanagedArrayPointer = Marshal.AllocHGlobal(size);

		IntPtr currentPtr = unmanagedArrayPointer;

		foreach (T element in array)
		{
			Marshal.StructureToPtr(element, currentPtr, false);

			currentPtr = IntPtr.Add(currentPtr, Marshal.SizeOf(typeof(T)));
		}

		return unmanagedArrayPointer;
	}


	public static T FromIntPtrStruct<T>(IntPtr ptr) where T : struct
	{
		T str = Marshal.PtrToStructure<T>(ptr);
		return str;
	}
	public static T[] FromIntPtrStructArray<T>(IntPtr ptr, int length) where T : struct
	{
		T[] array = new T[length];

		int size = Marshal.SizeOf(typeof(T));

		for (int i = 0; i < length; i++)
		{
			IntPtr currentPtr = IntPtr.Add(ptr, size * i);
			array[i] = Marshal.PtrToStructure<T>(currentPtr);
		}

		return array;
	}

	public static T FromIntPtr<T>(IntPtr ptr) where T : class
	{
		GCHandle handle = GCHandle.FromIntPtr(ptr);
		return handle.Target as T;
	}
}
