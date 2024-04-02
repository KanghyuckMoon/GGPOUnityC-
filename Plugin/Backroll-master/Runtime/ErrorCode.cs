using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class GGPOERRORCODE
{
	public const int OK = 0;
	public const int INVALID_HANDLE = -1;

	public const int ERRORCODE_SUCCESS = 0;
	public const int ERRORCODE_GENERAL_FAILURE = -1;
	public const int ERRORCODE_INVALID_SESSION = 1;
	public const int ERRORCODE_INVALID_PLAYER_HANDLE = 2;
	public const int ERRORCODE_PLAYER_OUT_OF_RANGE = 3;
	public const int ERRORCODE_PREDICTION_THRESHOLD = 4;
	public const int ERRORCODE_UNSUPPORTED = 5;
	public const int ERRORCODE_NOT_SYNCHRONIZED = 6;
	public const int ERRORCODE_IN_ROLLBACK = 7;
	public const int ERRORCODE_INPUT_DROPPED = 8;
	public const int ERRORCODE_PLAYER_DISCONNECTED = 9;
	public const int ERRORCODE_TOO_MANY_SPECTATORS = 10;
	public const int ERRORCODE_INVALID_REQUEST = 11;

	public static bool ERROR_SUCCEEDED(int result)
	{
		return result == ERRORCODE_SUCCESS;
	}
}
