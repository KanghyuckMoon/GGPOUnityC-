using UnityEngine;
using UnityEngine.Assertions;

namespace HouraiTeahouse.Backroll
{

	public unsafe class InputQueue
	{
		public const int INPUT_QUEUE_LENGTH = 128;
		int _id, _head, _tail, _length;
		bool _firstFrame;

		int _lastUserAddedFrame, _last_added_frame, _first_incorrectFrame;
		int _last_frame_requested;

		public int _frame_delay { get; set; }

		readonly GameInput[] _inputs;
		GameInput _prediction;

		int PreviousFrame(int offset)
		{
			return (offset == 0) ? (INPUT_QUEUE_LENGTH - 1) : (offset - 1);
		}

		public InputQueue(uint input_size, int id = -1)
		{
			_id = id;
			_head = _tail = _length = _frame_delay = 0;
			_firstFrame = true;
			_lastUserAddedFrame = GameInput.NullFrame;
			_first_incorrectFrame = GameInput.NullFrame;
			_last_frame_requested = GameInput.NullFrame;
			_last_added_frame = GameInput.NullFrame;

			_prediction = new GameInput(GameInput.NullFrame, null, input_size);

			_inputs = new GameInput[INPUT_QUEUE_LENGTH];
			for (var i = 0; i < _inputs.Length; i++)
			{
				_inputs[i].size = input_size;
			}
		}

		public int GetFirstIncorrectFrame()
		{
			return _first_incorrectFrame;
		}
		//GetLength
		//SetFrameDelay
		public void ResetPrediction(int frame)
		{
			Debug.Assert(_first_incorrectFrame == GameInput.NullFrame || frame <= _first_incorrectFrame);

			//Debug.Log($"resetting all prediction errors back to frame {frame}.");

			_prediction.frame = GameInput.NullFrame;
			_first_incorrectFrame = GameInput.NullFrame;
			_last_frame_requested = GameInput.NullFrame;
		}
		public void DiscardConfirmedFrames(int frame)
		{
			Debug.Assert(frame >= 0);

			if (_last_frame_requested != GameInput.NullFrame)
			{
				frame = Mathf.Min(frame, _last_frame_requested);
			}

			//Debug.Log($"discarding confirmed frames up to {frame} (last_added:{_last_added_frame} length:{_length} [head:{_head} tail:{_tail}");
			if (frame >= _last_added_frame)
			{
				_tail = _head;
			}
			else
			{
				int offset = frame - _inputs[_tail].frame + 1;

				//Debug.Log($"difference of {offset} frames.");
				Debug.Assert(offset >= 0);

				_tail = (_tail + offset) % INPUT_QUEUE_LENGTH;
				_length -= offset;
			}

			//Debug.Log($"after discarding, new tail is {_tail} (frame:{_inputs[_tail].frame}).");
			Debug.Assert(_length >= 0);
		}
		public bool GetInput(int requested_frame, ref GameInput input)
		{
			//Debug.Log($"requesting input frame {requested_frame}.");
			Debug.Assert(_first_incorrectFrame == GameInput.NullFrame);
			_last_frame_requested = requested_frame;
			Debug.Assert(requested_frame >= _inputs[_tail].frame);
			if (_prediction.frame == GameInput.NullFrame)
			{
				int offset = requested_frame - _inputs[_tail].frame;
				if (offset < _length)
				{
					offset = (offset + _tail) % INPUT_QUEUE_LENGTH;
					Debug.Assert(_inputs[offset].frame == requested_frame);
					input = _inputs[offset];
					//Debug.Log($"returning confirmed frame number {input.frame}.");
					return true;
				}

				if (requested_frame == 0)
				{
					//Debug.Log("basing new prediction frame from nothing, you're client wants frame 0.");
					_prediction.erase();
				}
				else if (_last_added_frame == GameInput.NullFrame)
				{
					//Debug.Log("basing new prediction frame from nothing, since we have no frames yet.");
					_prediction.erase();
				}
				else
				{
					//Debug.Log($"basing new prediction frame from previously added frame (queue entry:{PreviousFrame(_head)}, frame:{_inputs[PreviousFrame(_head)].frame}");
					_prediction = _inputs[PreviousFrame(_head)];
				}
				_prediction.frame++;
			}

			Debug.Assert(_prediction.frame >= 0);

			input = _prediction;
			input.frame = requested_frame;
			//Debug.Log($"returning prediction frame number {input.frame} {_prediction.frame}");

			return false;
		}
		public void AddInput(ref GameInput input)
		{
			int new_frame;
			//Debug.Log($"adding input frame number {input.frame} to queue.");
			Debug.Assert(_lastUserAddedFrame == GameInput.NullFrame || input.frame == _lastUserAddedFrame + 1);
			_lastUserAddedFrame = input.frame;

			new_frame = AdvanceQueueHead(input.frame);
			if (new_frame != GameInput.NullFrame)
			{
				AddDelayedInputToQueue(input, new_frame);
			}

			//Debug.Log($"Origin Frame {input.frame} Change Frame {new_frame}");
			input.frame = new_frame;
		}
		protected int AdvanceQueueHead(int frame)
		{
			//Debug.Log($"advancing queue head to frame {frame}.");

			int expected_frame = _firstFrame ? 0 : _inputs[PreviousFrame(_head)].frame + 1;

			frame += _frame_delay;

			if (expected_frame > frame)
			{
				//Debug.Log($"Dropping input frame {frame} (expected next frame to be {expected_frame}).");
				return GameInput.NullFrame;
			}

			while (expected_frame < frame)
			{
				//Debug.Log($"Adding padding frame {expected_frame} to account for change in frame delay.");
				ref GameInput last_frame = ref _inputs[PreviousFrame(_head)];
				AddDelayedInputToQueue(last_frame, expected_frame);
				expected_frame++;
			}

			Debug.Assert(frame == 0 || frame == _inputs[PreviousFrame(_head)].frame + 1);
			return frame;
		}
		protected void AddDelayedInputToQueue(in GameInput input, int frame_number)
		{
			//Debug.Log($"adding delayed input frame number {frame_number} to queue.");
			Debug.Assert(input.size == _prediction.size);
			if (input.size == _prediction.size)
			{
				//Debug.Log($"inputSize {input.size} predictionSize {_prediction.size}");
			}
			Debug.Assert(_last_added_frame == GameInput.NullFrame || frame_number == _last_added_frame + 1);
			Debug.Assert(frame_number == 0 || _inputs[PreviousFrame(_head)].frame == frame_number - 1);

			_inputs[_head] = input;
			_inputs[_head].frame = frame_number;
			_head = (_head + 1) % INPUT_QUEUE_LENGTH;
			_length++;
			_firstFrame = false;

			_last_added_frame = frame_number;

			if (_prediction.frame != GameInput.NullFrame)
			{
				Debug.Assert(frame_number == _prediction.frame);

				if (_first_incorrectFrame == GameInput.NullFrame && !_prediction.equal(input, true))
				{
					//Debug.Log($"frame {frame_number} does not match prediction.  marking error.");
					_first_incorrectFrame = frame_number;
				}

				if (_prediction.frame == _last_frame_requested && _first_incorrectFrame == GameInput.NullFrame)
				{
					//Debug.Log("prediction is correct!  dumping out of prediction mode.");
					_prediction.frame = GameInput.NullFrame;
				}
				else
				{
					_prediction.frame++;
				}
			}
			Debug.Assert(_length <= INPUT_QUEUE_LENGTH);
		}
		//Log
	}


}
