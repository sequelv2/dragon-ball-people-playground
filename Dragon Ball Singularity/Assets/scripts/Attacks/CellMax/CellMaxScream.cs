using Mod;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using static FunLittleGames.Linkage.Linkage;
using Random = UnityEngine.Random;
using HarmonyLib;

namespace DBPU
{
    public class CellMaxScream : MonoBehaviour
    {
        public bool isCreaming = false;
        public bool ForceScreaming = false;
        public Sprite[] Sprites = new Sprite[5];
        public AudioClip Scream;

        private PhysicalBehaviour phys;
        private PersonBehaviour person;
        private bool isShaking = false;
        Coroutine beamMouthLoop;
        Coroutine activeScreamRoutine;
        Coroutine screamFadeRoutine;

        private void Start()
        {
            phys = gameObject.GetComponent<PhysicalBehaviour>();
            person = transform.root.gameObject.GetComponent<PersonBehaviour>();
        }

        public void PlayForcedScream()
        {
            if (!isCreaming)
                activeScreamRoutine = StartCoroutine(StartScream());
        }

        public void FadeOutWithBeam()
        {
            ForceScreaming = false;

            if (beamMouthLoop != null)
            {
                StopCoroutine(beamMouthLoop);
                beamMouthLoop = null;
            }

            if (activeScreamRoutine != null)
            {
                StopCoroutine(activeScreamRoutine);
                activeScreamRoutine = null;
            }

            isShaking = false;
            isCreaming = false;
            SetFaceSprite(0);

            if (screamFadeRoutine != null)
                StopCoroutine(screamFadeRoutine);
            screamFadeRoutine = StartCoroutine(FadeScreamAudio());
        }

        IEnumerator FadeScreamAudio()
        {
            Transform screamNode = transform.Find("Scream");
            if (screamNode == null)
            {
                screamFadeRoutine = null;
                yield break;
            }

            AudioSource audioSource = screamNode.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                Destroy(screamNode.gameObject);
                screamFadeRoutine = null;
                yield break;
            }

            while (audioSource.volume > 0.05f)
            {
                audioSource.volume -= 0.15f;
                yield return new WaitForSeconds(0.025f);
            }

            audioSource.Stop();
            Destroy(screamNode.gameObject);
            screamFadeRoutine = null;
        }

        public IEnumerator PrepareForBeam()
        {
            while (isCreaming) yield return null;

            isCreaming = true;
            MCP_Utils.PlayAudio(transform.position + (transform.up * -3.5f) + (transform.right * 1f), Scream, transform, spatialBlend: 0.7f, name: "Scream");

            for (int i = 1; i < 3; i++)
            {
                SetFaceSprite(i);
                yield return new WaitForSeconds(0.04f);
            }

            isShaking = true;
            ForceScreaming = true;

            if (beamMouthLoop != null) StopCoroutine(beamMouthLoop);
            beamMouthLoop = StartCoroutine(BeamMouthLoop());

            yield return new WaitForSeconds(0.08f);
        }

        IEnumerator BeamMouthLoop()
        {
            int face = 4;
            while (ForceScreaming)
            {
                SetFaceSprite(face);
                face = face == 4 ? 3 : 4;
                yield return new WaitForSeconds(0.06f);
            }
        }

        public IEnumerator FinishBeamMouth()
        {
            FadeOutWithBeam();
            yield return null;
        }

        public void CancelBeamMouth()
        {
            FadeOutWithBeam();
        }

        private void Update()
        {
            if (isCreaming || !person.IsAlive())
            {
                if (isShaking) CameraShakeBehaviour.main.Shake(2f, transform.position);

                if (transform.Find("Scream"))
                {
                    AudioSource audioSource = transform.Find("Scream").GetComponent<AudioSource>();
                    if (audioSource.volume != DBPU.VoiceLineVolume) audioSource.volume = DBPU.VoiceLineVolume;
                }

                return;
            }

            if (!person.IsAlive()) return;

            if (DbpuInputUtil.Down(DBPU.CellMaxScreamActionId, KeyCode.R) && CeceUtils.IsSelected(phys))
            {
                isCreaming = true;
                activeScreamRoutine = StartCoroutine(StartScream());
            }
        }

        void SetFaceSprite(int index)
        {
            GetComponent<SpriteRenderer>().sprite = Sprites[index];
            MCP_Utils.SafeRefreshOutline(phys);
        }

        public IEnumerator StartScream()
        {
            isCreaming = true;
            MCP_Utils.PlayAudio(transform.position + (transform.up * -3.5f) + (transform.right * 1f), Scream, transform, spatialBlend: 0.7f, name: "Scream");

            for (int i = 1; i < 3; i++)
            {
                SetFaceSprite(i);
                yield return new WaitForSeconds(0.02f);
            }

            isShaking = true;

            int face = 4;
            int ticks = 0;
            while (ForceScreaming || ticks < 67)
            {
                SetFaceSprite(face);
                face = face == 4 ? 3 : 4;
                ticks++;
                yield return new WaitForSeconds(0.06f);
            }

            isShaking = false;

            for (int i = 3; i >= 0; i--)
            {
                SetFaceSprite(i);
                yield return new WaitForSeconds(0.1f);
            }

            yield return new WaitForSeconds(0.5f);

            isCreaming = false;
            ForceScreaming = false;
            activeScreamRoutine = null;

            yield return null;
        }
    }
}
