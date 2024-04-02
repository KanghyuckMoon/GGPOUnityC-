using System;
using UnityEngine;

namespace HouraiTeahouse.Backroll
{

	public class TimeSync
	{


		public const int kDefaultFrameWindowSize = 40;
		public const int kDefaultMinUniqueFrames = 10;
		public const int kDefaultMinFrameAdvantage = 3;
		public const int kDefaultMaxFrameAdvantage = 9;

		protected readonly int _minFrameAdvantage;
		protected readonly int _maxFrameAdvantage;

		protected readonly int[] _local;
		protected readonly int[] _remote;
		protected readonly GameInput[] _last_inputs;
		protected int _next_prediction;

		public TimeSync(int frameWindowSize = kDefaultFrameWindowSize,
						int minUniqueFrames = kDefaultMinUniqueFrames,
						int minFrameAdvantage = kDefaultMinFrameAdvantage,
						int maxFrameAdvantage = kDefaultMaxFrameAdvantage)
		{
			_local = new int[frameWindowSize];
			_remote = new int[frameWindowSize];
			_next_prediction = frameWindowSize * 3;

			_minFrameAdvantage = minFrameAdvantage;
			_maxFrameAdvantage = maxFrameAdvantage;
			_last_inputs = new GameInput[minUniqueFrames];
		}

		public void AdvanceFrame(ref GameInput input, int advantage, int radvantage)
		{
			// Remember the last frame and frame advantage
			_last_inputs[input.frame % _last_inputs.Length] = input;
			_local[input.frame % _local.Length] = advantage;
			_remote[input.frame % _remote.Length] = radvantage;
		}

		public int RecommendFrameWaitDuration(bool require_idle_input)
		{
			// Average our local and remote frame advantages
			int i, sum = 0;
			float advantage, radvantage;
			for (i = 0; i < _local.Length; i++)
			{
				sum += _local[i];
			}
			advantage = sum / (float)_local.Length;

			sum = 0;
			for (i = 0; i < _remote.Length; i++)
			{
				sum += _remote[i];
			}
			radvantage = sum / (float)_remote.Length;

			if (advantage >= radvantage)
			{
				return 0;
			}

			int sleep_frames = (int)(((radvantage - advantage) / 2) + 0.5);

			//Debug.LogFormat("iteration {}:  sleep frames is {}", count, sleep_frames);
			//Debug.Log($"iteration {count}:  sleep frames is {sleep_frames}");

			if (sleep_frames < _minFrameAdvantage)
			{
				return 0;
			}

			if (require_idle_input)
			{
				for (i = 1; i < _last_inputs.Length; i++)
				{
					if (!_last_inputs[i].equal(_last_inputs[0], true))
					{
						//Debug.Log($"iteration {count}:  sleep frames is {sleep_frames}");
						return 0;
					}
				}
			}

			return Math.Min(sleep_frames, _maxFrameAdvantage);
		}

	}

}
