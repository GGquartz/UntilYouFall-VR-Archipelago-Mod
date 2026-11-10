using UnityEngine;
using Object = UnityEngine.Object;

namespace UntilYouFallArchipelago;

internal sealed class NoticeSound
{
    public const string DefaultClipName = "ui_CLAY_CompleteBar_01";
    private const string TrapClipName = "sfx_CLAY_super_vicious_strike_fail";
    private const float DeathPitch = 0.55f;

    private readonly LogChannel _log;
    private AudioSource? _source;

    public NoticeSound(Action<string> log) => _log = log.Channel("APNoticeSound");

    public void Play(ApNotice notice)
    {
        PlayClip(notice.Trap ? TrapClipName : DefaultClipName, notice.Death ? DeathPitch : 1f);
    }

    private void PlayClip(string name, float pitch)
    {
        try
        {
            var clip = SceneCache.Clip(name);
            if (clip == null)
            {
                _log.Write($"Clip not loaded name={name}");
                return;
            }
            if (_source == null)
            {
                var host = new GameObject("Archipelago Notice Sound");
                Object.DontDestroyOnLoad(host);
                _source = host.AddComponent<AudioSource>();
                _source.spatialBlend = 0;
                _source.playOnAwake = false;
            }
            if (clip.loadState == AudioDataLoadState.Unloaded)
                clip.LoadAudioData();
            _source.pitch = pitch;
            _source.PlayOneShot(clip);

            _log.Write($"Played name={name} length={clip.length:F2} loadState={clip.loadState}");
        }
        catch (Exception ex)
        {
            _log.Write("Failed: " + ex.Message);
        }
    }
}
