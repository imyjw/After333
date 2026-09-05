using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public sealed class BattleSfxPlayer : MonoBehaviour
{
    [SerializeField] private AudioClip _cardDrawClip;
    [SerializeField, Range(0f, 1f)] private float _cardDrawVolume = 1f;
    [SerializeField] private float _repeatedDrawInterval = 0.12f;

    private AudioSource _audioSource;
    private Coroutine _drawRoutine;
    private int _pendingDrawSounds;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.loop = false;
        _audioSource.spatialBlend = 0f;
    }

    public void QueueCardDraw(int count)
    {
        _pendingDrawSounds += Mathf.Max(0, count);

        if (_drawRoutine == null && _pendingDrawSounds > 0)
        {
            _drawRoutine = StartCoroutine(PlayDrawQueue());
        }
    }

    private IEnumerator PlayDrawQueue()
    {
        while (_pendingDrawSounds > 0)
        {
            _pendingDrawSounds--;
            _audioSource.PlayOneShot(_cardDrawClip, _cardDrawVolume);

            if (_pendingDrawSounds > 0)
            {
                yield return new WaitForSecondsRealtime(_repeatedDrawInterval);
            }
        }

        _drawRoutine = null;
    }
}