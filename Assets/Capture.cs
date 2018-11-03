using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Capture : MonoBehaviour, ITimeSource
{
	public int numFrames = 120;
	public int frameRate = 30;

	private int currentFrame = 0;

	void Start()
	{
		currentFrame = 0;
	}

	void Update()
	{
		if (currentFrame < numFrames)
		{
			string fileName = string.Format("{0:D4}.png", currentFrame+1);
			ScreenCapture.CaptureScreenshot(fileName);
			Debug.Log(fileName);
		}
		currentFrame++;
	}

	public float GetTime()
	{
		return (float)currentFrame / numFrames * frameRate;
	}
}
