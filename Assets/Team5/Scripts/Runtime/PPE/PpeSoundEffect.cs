using UnityEngine;

namespace Team5.qPCR
{
    public enum PpeSoundKind { Coat, Glove, Soap, Tap, Dryer }
    [RequireComponent(typeof(AudioSource))]
    public sealed class PpeSoundEffect : MonoBehaviour
    {
        [SerializeField] private PpeSoundKind kind;
        private AudioClip generated;
        private void Awake()
        {
            var source=GetComponent<AudioSource>();
            if(source.clip!=null)return;
            const int rate=22050;
            var duration=kind==PpeSoundKind.Dryer?1f:kind==PpeSoundKind.Coat?.35f:kind==PpeSoundKind.Glove?.22f:.12f;
            var samples=new float[Mathf.RoundToInt(duration*rate)];var random=new System.Random(53+(int)kind);var filtered=0f;
            for(var i=0;i<samples.Length;i++)
            {
                var time=i/(float)rate;filtered=filtered*.88f+((float)random.NextDouble()*2-1)*.12f;
                var envelope=kind==PpeSoundKind.Dryer?1f:Mathf.Sin(Mathf.PI*i/samples.Length);
                samples[i]=(filtered*.22f+(kind==PpeSoundKind.Dryer?Mathf.Sin(time*Mathf.PI*2*120)*.018f:0))*envelope;
            }
            generated=AudioClip.Create("PPE "+kind+" effect",samples.Length,1,rate,false);generated.SetData(samples,0);source.clip=generated;
        }
        private void OnDestroy(){if(generated!=null)Destroy(generated);}
    }
}
