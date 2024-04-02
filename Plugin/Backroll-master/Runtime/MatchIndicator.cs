using JetBrains.Annotations;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Match
{
	public class MatchIndicator : MonoBehaviour
	{
		[SerializeField]
		public class MisMatchFrame
		{
			public int frame;
			public string original;
			public string reply;
		}

		// isMatch 변수를 public으로 설정하거나, 다른 스크립트에서 접근 가능하게 설정해야 합니다.
		public static bool isMatch = false; // 이 값을 true 또는 false로 변경하여 테스트해보세요.
		public static List<MisMatchFrame> misMatchFrames = new List<MisMatchFrame>();

		public static void AddMisMatchFrame(int frame, string original = null, string reply = null)
		{
			MisMatchFrame misMatchFrame = new MisMatchFrame();
			misMatchFrame.frame = frame;
			misMatchFrame.original = original;
			misMatchFrame.reply = reply;
			misMatchFrames.Add(misMatchFrame);
		}

		public static void Export()
		{

		}

		private void OnGUI()
		{
			GUIStyle guiStyle = new GUIStyle(GUI.skin.label);
			guiStyle.fontSize = 25;
			guiStyle.alignment = TextAnchor.MiddleCenter;

			// 프레임 번호 표시를 위한 스타일
			GUIStyle frameStyle = new GUIStyle(GUI.skin.label)
			{
				fontSize = 20, // 프레임 번호는 조금 더 작게 표시
				alignment = TextAnchor.UpperCenter,
				normal = { textColor = Color.red } // 빨간색 글자
			};

			// 배경색과 텍스트 색상 설정
			Color backgroundColor = Color.black;
			Color textColor = isMatch ? Color.green : Color.red;
			guiStyle.normal.background = MakeTex(600, 1, backgroundColor);
			guiStyle.normal.textColor = textColor;

			// 화면의 우측 하단에 "Match" 또는 "MisMatch" 메시지 표시
			Rect matchPosition = new Rect(Screen.width - 110, Screen.height - 40, 100, 30);
			string message = isMatch ? "Match" : "MisMatch";
			GUI.Label(matchPosition, message, guiStyle);

			// misMatchFrames에 요소가 1개 이상 있는 경우에만 버튼 표시
			if (misMatchFrames.Count > 0)
			{
				Rect buttonPosition = new Rect(Screen.width - 210, Screen.height - 40, 100, 30);
				if (GUI.Button(buttonPosition, "Save Log"))
				{
					SaveMisMatchFrames();
				}
			}

			// 최대 5개의 프레임 번호 표시
			int displayCount = Mathf.Min(misMatchFrames.Count, 5);
			for (int i = 0; i < displayCount; i++)
			{
				MisMatchFrame frame = misMatchFrames[misMatchFrames.Count - 1 - i]; // 리스트 끝에서부터 역순으로
				Rect framePosition = new Rect(Screen.width - 110, Screen.height - 80 - (i * 20), 100, 30); // 각 프레임 번호 위치 조정
				GUI.Label(framePosition, frame.frame.ToString(), frameStyle);
			}
		}

		// 단색 텍스처 생성 함수
		private Texture2D MakeTex(int width, int height, Color col)
		{
			Color[] pix = new Color[width * height];
			for (int i = 0; i < pix.Length; ++i)
			{
				pix[i] = col;
			}
			Texture2D result = new Texture2D(width, height);
			result.SetPixels(pix);
			result.Apply();
			return result;
		}


		private void SaveMisMatchFrames()
		{
			// 텍스트 파일로 저장할 문자열 생성
			StringBuilder sb = new StringBuilder();
			foreach (MisMatchFrame frame in misMatchFrames)
			{
				if(frame.original == null || frame.reply == null)
				{
					continue;
					if(frame.original == null)
					{
						sb.AppendLine($"Reply Frame: {frame.frame}, Reply: {frame.reply}");
					}
					else
					{
						sb.AppendLine($"Original Frame: {frame.frame}, Original: {frame.original}");
					}
				}
				else
				{
					// original과 reply를 줄 단위로 나눕니다.
					string[] originalLines = frame.original.Split('\n');
					string[] replyLines = frame.reply.Split('\n');

					// 두 배열의 길이가 다를 수 있으므로, 더 짧은 길이를 기준으로 비교합니다.
					int minLength = Mathf.Min(originalLines.Length, replyLines.Length);

					for (int i = 0; i < minLength; i++)
					{
						if (originalLines[i] != replyLines[i])
						{
							// original과 reply가 다른 경우, 해당 줄을 StringBuilder에 추가합니다.
							sb.AppendLine($"Frame: {frame.frame}, Original: {originalLines[i]}, Reply: {replyLines[i]}");
						}
					}
				}

				sb.AppendLine("\n---------------\n");
			}


			// 텍스트 파일로 저장할 문자열 생성
			StringBuilder sb2 = new StringBuilder();
			foreach (MisMatchFrame frame in misMatchFrames)
			{
				if (frame.original == null || frame.reply == null)
				{
					if (frame.original == null)
					{
						sb2.AppendLine($"Reply Frame: {frame.frame}, Reply: {frame.reply}");
					}
					else
					{
						sb2.AppendLine($"Onriginal Frame: {frame.frame}, Original: {frame.original}");
					}
				}
				else
				{
					sb2.AppendLine($"Frame: {frame.frame}, Original: {frame.original}, Reply: {frame.reply}");
				}
			}


			// 파일 경로 설정 (여기서는 프로젝트 폴더의 Assets 폴더 안에 저장)
			string path = Path.Combine(Application.dataPath, "MisMatchFramesLog.txt");
			string path2 = Path.Combine(Application.dataPath, "MisMatchFramesLogFull.txt");

			// 파일 쓰기
			File.WriteAllText(path, sb.ToString());
			File.WriteAllText(path2, sb2.ToString());

			// 에디터에서 변경 사항을 강제로 갱신
#if UNITY_EDITOR
			UnityEditor.AssetDatabase.Refresh();
#endif

			Debug.Log($"MisMatchFrames log saved to {path}");
			Debug.Log($"MisMatchFrames log saved to {path2}");
		}
	}

}
