#if UNITY_EDITOR
using System;
using System.Collections;
using UnityEngine;

namespace Astral.EditorTools.GifRecorder
{
    public sealed class GifCapturePump : MonoBehaviour
    {
        public Action Capture;
        IEnumerator Start()
        {
            var endOfFrame = new WaitForEndOfFrame();
            while (true) { yield return endOfFrame; Capture?.Invoke(); }
        }
    }
}
#endif
