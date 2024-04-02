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

		// isMatch ������ public���� �����ϰų�, �ٸ� ��ũ��Ʈ���� ���� �����ϰ� �����ؾ� �մϴ�.
		public static bool isMatch = false; // �� ���� true �Ǵ� false�� �����Ͽ� �׽�Ʈ�غ�����.
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

			// ������ ��ȣ ǥ�ø� ���� ��Ÿ��
			GUIStyle frameStyle = new GUIStyle(GUI.skin.label)
			{
				fontSize = 20, // ������ ��ȣ�� ���� �� �۰� ǥ��
				alignment = TextAnchor.UpperCenter,
				normal = { textColor = Color.red } // ������ ����
			};

			// ������ �ؽ�Ʈ ���� ����
			Color backgroundColor = Color.black;
			Color textColor = isMatch ? Color.green : Color.red;
			guiStyle.normal.background = MakeTex(600, 1, backgroundColor);
			guiStyle.normal.textColor = textColor;

			// ȭ���� ���� �ϴܿ� "Match" �Ǵ� "MisMatch" �޽��� ǥ��
			Rect matchPosition = new Rect(Screen.width - 110, Screen.height - 40, 100, 30);
			string message = isMatch ? "Match" : "MisMatch";
			GUI.Label(matchPosition, message, guiStyle);

			// misMatchFrames�� ��Ұ� 1�� �̻� �ִ� ��쿡�� ��ư ǥ��
			if (misMatchFrames.Count > 0)
			{
				Rect buttonPosition = new Rect(Screen.width - 210, Screen.height - 40, 100, 30);
				if (GUI.Button(buttonPosition, "Save Log"))
				{
					SaveMisMatchFrames();
				}
			}

			// �ִ� 5���� ������ ��ȣ ǥ��
			int displayCount = Mathf.Min(misMatchFrames.Count, 5);
			for (int i = 0; i < displayCount; i++)
			{
				MisMatchFrame frame = misMatchFrames[misMatchFrames.Count - 1 - i]; // ����Ʈ ���������� ��������
				Rect framePosition = new Rect(Screen.width - 110, Screen.height - 80 - (i * 20), 100, 30); // �� ������ ��ȣ ��ġ ����
				GUI.Label(framePosition, frame.frame.ToString(), frameStyle);
			}
		}

		// �ܻ� �ؽ�ó ���� �Լ�
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
			// �ؽ�Ʈ ���Ϸ� ������ ���ڿ� ����
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
					// original�� reply�� �� ������ �����ϴ�.
					string[] originalLines = frame.original.Split('\n');
					string[] replyLines = frame.reply.Split('\n');

					// �� �迭�� ���̰� �ٸ� �� �����Ƿ�, �� ª�� ���̸� �������� ���մϴ�.
					int minLength = Mathf.Min(originalLines.Length, replyLines.Length);

					for (int i = 0; i < minLength; i++)
					{
						if (originalLines[i] != replyLines[i])
						{
							// original�� reply�� �ٸ� ���, �ش� ���� StringBuilder�� �߰��մϴ�.
							sb.AppendLine($"Frame: {frame.frame}, Original: {originalLines[i]}, Reply: {replyLines[i]}");
						}
					}
				}

				sb.AppendLine("\n---------------\n");
			}


			// �ؽ�Ʈ ���Ϸ� ������ ���ڿ� ����
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


			// ���� ��� ���� (���⼭�� ������Ʈ ������ Assets ���� �ȿ� ����)
			string path = Path.Combine(Application.dataPath, "MisMatchFramesLog.txt");
			string path2 = Path.Combine(Application.dataPath, "MisMatchFramesLogFull.txt");

			// ���� ����
			File.WriteAllText(path, sb.ToString());
			File.WriteAllText(path2, sb2.ToString());

			// �����Ϳ��� ���� ������ ������ ����
#if UNITY_EDITOR
			UnityEditor.AssetDatabase.Refresh();
#endif

			Debug.Log($"MisMatchFrames log saved to {path}");
			Debug.Log($"MisMatchFrames log saved to {path2}");
		}
	}

}
