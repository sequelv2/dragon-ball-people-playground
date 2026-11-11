using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace DBPU
{
    // Opt-in visual verification in the disposable native game process only.
    public sealed partial class KiCombatQa
    {
        IEnumerator VerifyKiFistVisuals()
        {
            GameObject fighterRoot=null,victimRoot=null,background=null;
            PersonBehaviour fighter=null,victim=null;KiSystem energy=null,victimEnergy=null;
            KiHandLoadout loadout=null;KiFistHand fist=null;LimbBehaviour probe=null;
            AllyKiReservoir received=null;KiSpaceRefraction refraction=null;
            Vector2 refractionProbePoint=new Vector2(1.1f,32.5f);
            if(!Check(()=>
            {
                foreach(var actor in FindObjectsOfType<PersonBehaviour>())actor.transform.position+=Vector3.left*180;
                DBPU.KiDrainMultiplier=0;DBPU.KiAttackCostMultiplier=1;DBPU.DamageMultiplier=1;
                fighterRoot=Spawn("[DBPU] (BM) Goku",new Vector3(-1.65f,30,0));
                victimRoot=Spawn("[DBPU] (BM) Goku",new Vector3(1.65f,30,0));
                var camera=Global.main.camera;camera.transform.position=new Vector3(0,31,-10);
                camera.orthographicSize=4.5f;camera.allowHDR=true;
                background=FistVisualBackdrop(new Vector3(0,31,7));
                Log("VISUAL PIPELINE "+(GraphicsSettings.currentRenderPipeline?GraphicsSettings.currentRenderPipeline.name:"built-in")+" / graphics="+SystemInfo.graphicsDeviceType+" / device="+SystemInfo.graphicsDeviceName);
                Log("VISUAL MSAA camera.allowMSAA="+camera.allowMSAA+" / QualitySettings.antiAliasing="+QualitySettings.antiAliasing);
            }))yield break;
            yield return new WaitForSecondsRealtime(1.6f);
            if(!Check(()=>
            {
                Prepare(fighterRoot,out fighter,out energy);Prepare(victimRoot,out victim,out victimEnergy);
                foreach(var actor in new[]{fighterRoot,victimRoot})
                    foreach(var renderer in actor.GetComponentsInChildren<SpriteRenderer>())renderer.sharedMaterial=new Material(Shader.Find("Sprites/Default"));
                foreach(var canvas in FindObjectsOfType<Canvas>())canvas.enabled=false;
                received=AllyKiReservoir.GetOrCreate(fighter);received.ConsumeSupport(received.TotalReceived);
                loadout=GetHand(fighter,0);Assert(loadout.Equip(KiHandLoadout.FistId),"Cannot equip native visual QA fist");
                fist=KiFistHand.GetOrCreate(loadout.Limb);probe=victim.Limbs.First(l=>l.name.Contains("LowerArm"));
                Assert(KiCombatVfxAssets.Ready,"Native combat bundle/materials are unavailable");
                Assert(KiSpaceRefraction.Ready,"Native space refraction shader is unavailable");
                Assert(!GraphicsSettings.currentRenderPipeline,"Screen refraction QA expects the native built-in camera pipeline");
                energy.CurrentForm="Base";energy.currentKi=1000;ShowFistForQa(fist);
            }))yield break;
            yield return null;yield return new WaitForEndOfFrame();
            if(!Check(()=>Capture("40-fist-ready-native-volume.png")))yield break;
            if(!Check(()=>
            {
                Assert(fist.BeginHeldCharge(),"Native visual charge did not start");
                for(int i=0;i<15;i++)fist.ChargeStep(.1f);ShowFistForQa(fist);
            }))yield break;
            yield return new WaitForSeconds(.18f);yield return new WaitForEndOfFrame();
            if(!Check(()=>
            {
                Capture("41-fist-charge-volume.png");
                for(int i=0;i<46;i++)fist.ChargeStep(.1f);fist.ReleaseHeldCharge();ShowFistForQa(fist);
                Assert(fist.IsOvercharged,"Native visual full charge was not armed");
            }))yield break;
            yield return new WaitForSeconds(.12f);yield return new WaitForEndOfFrame();
            if(!Check(()=>
            {
                Capture("42-fist-full-charge-volume.png");FistAssertNativeMaterials(fist);
            }))yield break;
            yield return FistVerifyMirroredTrail(fighterRoot,fist);
            if(finished)yield break;
            if(!Check(()=>
            {
                ResetFistProbe(probe,fighter);Assert(fist.DischargeForTest(probe,probe.transform.position),"Native full-charge visual strike failed");
            }))yield break;
            yield return new WaitForSeconds(.055f);yield return new WaitForEndOfFrame();
            if(!Check(()=>Capture("43-fist-actual-hit-flash.png")))yield break;
            yield return new WaitForSeconds(.22f);yield return new WaitForEndOfFrame();
            if(!Check(()=>Capture("44-fist-actual-hit-pressure.png")))yield break;
            yield return new WaitForSeconds(1.9f);
            if(!Check(()=>
            {
                energy.CurrentForm="SSJBlue";energy.currentKi=1000;
                Assert(fist.SetMode(KiFistMode.God),"Divine visual mode did not become available");
                ModesCharge(fist,61);ShowFistForQa(fist);
            }))yield break;
            yield return new WaitForSeconds(.15f);yield return new WaitForEndOfFrame();
            if(!Check(()=>
            {
                Capture("45-god-fist-native-volume.png");FistAssertNativeMaterials(fist);fist.Cancel();
                energy.CurrentForm="Base";energy.currentKi=1000;received.ReturnSupport(250);
                Assert(fist.SetMode(KiFistMode.Infusion),"Infusion visual mode did not activate");
                ModesCharge(fist,61);ShowFistForQa(fist);
                Assert(fist.InfusedEnergy>=249,"Infusion visual preview has no reserved energy");
            }))yield break;
            yield return new WaitForSeconds(.15f);yield return new WaitForEndOfFrame();
            if(!Check(()=>
            {
                Capture("46-infusion-fist-native-volume.png");FistAssertNativeMaterials(fist);
                fist.Cancel();loadout.Equip(KiHandLoadout.NoneId);
                Log("PASS native ready/charging/full-charge/God/Infusion volume materials and actual overcharged hit captures");
            }))yield break;
            // Record distinct temporal stages for all public impact tiers on a detailed background.
            for(int tier=0;tier<3;tier++)
            {
                int captureTier=tier;
                if(!Check(()=>KiImpactBurst.Spawn(new Vector2(0,31),captureTier,true,false,Vector2.right,
                    captureTier==2?new Color(1,.88f,.48f):captureTier==1?new Color(.32f,.85f,1):new Color(.8f,.92f,1))))yield break;
                yield return new WaitForSeconds(.08f);yield return new WaitForEndOfFrame();
                if(!Check(()=>Capture("47-tier-"+captureTier+"-impact-flash.png")))yield break;
                yield return new WaitForSeconds(.27f);yield return new WaitForEndOfFrame();
                if(!Check(()=>Capture("48-tier-"+captureTier+"-impact-pressure.png")))yield break;
                yield return new WaitForSeconds(.45f);yield return new WaitForEndOfFrame();
                if(!Check(()=>Capture("49-tier-"+captureTier+"-impact-aftershock.png")))yield break;
                if(tier==2)
                {
                    yield return new WaitForSeconds(.5f);yield return new WaitForEndOfFrame();
                    if(!Check(()=>Capture("49-tier-2-late-pressure-1_3s.png")))yield break;
                }
                yield return new WaitForSeconds(2.4f);
            }
            if(!Check(()=>
            {
                Assert(FindObjectsOfType<KiStrikeImpactVfx>().Length==0,"Impact volume survived its lifetime");
                Assert(KiSpaceRefraction.ActiveWaveCount==0,"Pressure waves survived their lifetime");
                foreach(var actor in FindObjectsOfType<PersonBehaviour>())actor.transform.position+=Vector3.left*180;
                KiSpaceRefraction.Pulse(refractionProbePoint,4.3f,.75f,1.15f,Vector2.right);
                refraction=Global.main.camera.GetComponent<KiSpaceRefraction>();
                Assert(refraction && KiSpaceRefraction.ActiveWaveCount>0,"Pulse did not attach a wave to the native camera");
            }))yield break;
            yield return new WaitForSeconds(.25f);
            float pausedWaveAge=0;
            if(!Check(()=>
            {
                Global.main.TogglePaused();pausedWaveAge=refraction.OldestWaveAge;
                Assert(pausedWaveAge>0,"Refraction pulse did not advance before pause");
            }))yield break;
            yield return new WaitForSecondsRealtime(.16f);
            if(!Check(()=>
            {
                Near(pausedWaveAge,refraction.OldestWaveAge,.0001f,"Paused refraction continued expanding");
                FistVerifyBackgroundRefraction(Global.main.camera,refraction,refractionProbePoint);
                Global.main.TogglePaused();
                Log("PASS real camera background refraction: emission absent, paired same-frame bypass comparison, paused wave age retained");
            }))yield break;
            yield return new WaitForSeconds(1.3f);
            if(!Check(()=>
            {
                Assert(KiSpaceRefraction.ActiveWaveCount==0,"Standalone pressure wave did not expire");
                for(int i=0;i<24;i++)KiSpaceRefraction.Pulse(new Vector2((i%5-2)*.2f,31),2,.02f,.3f,Vector2.right);
                Assert(KiSpaceRefraction.ActiveWaveCount>0 && KiSpaceRefraction.ActiveWaveCount<=12,"Refraction wave capacity is unbounded");
                Log("PASS wave expiry and bounded simultaneous refraction capacity: "+KiSpaceRefraction.ActiveWaveCount);
            }))yield break;
            yield return new WaitForSeconds(.45f);
            if(!Check(()=>
            {
                Assert(KiSpaceRefraction.ActiveWaveCount==0,"Capacity probe left active refraction waves");
                if(background)
                {
                    var material=background.GetComponent<MeshRenderer>().sharedMaterial;
                    Destroy(material.mainTexture);Destroy(material);Destroy(background.GetComponent<MeshFilter>().sharedMesh);Destroy(background);
                }
                if(fighterRoot)Destroy(fighterRoot);if(victimRoot)Destroy(victimRoot);
                Log("ALL KI FIST NATIVE VISUAL AND REFRACTION CHECKS PASSED");
            }))yield break;
        }

        void FistAssertNativeMaterials(KiFistHand fist)
        {
            var visual=fist.GetComponentInChildren<KiStrikeLimbVfx>(true);
            Assert(visual,"Charged limb native visual component is missing");
            var renderers=visual.GetComponentsInChildren<MeshRenderer>(true);
            Assert(renderers.Length>0,"Native charged volume has no mesh renderer");
            foreach(var renderer in renderers)
            {
                Assert(renderer.sharedMaterial && renderer.sharedMaterial.shader && renderer.sharedMaterial.shader.isSupported,"Native charged volume shader is unsupported");
                Assert(renderer.sharedMaterial.shader.name.StartsWith("DBPU/Combat/",StringComparison.Ordinal),"Ki Fist did not use its native Unity combat shader");
            }
            Assert(visual.GetComponentsInChildren<SpriteRenderer>(true).Length==0,"Ki Fist contains a sprite renderer");
            Assert(visual.GetComponentsInChildren<LineRenderer>(true).Length==0,"Ki Fist contains a primitive line renderer");
        }

        IEnumerator FistVerifyMirroredTrail(GameObject fighterRoot,KiFistHand fist)
        {
            Vector3 originalScale=Vector3.one,originalPoint=Vector3.zero,start=Vector3.zero;
            MeshFilter path=null;
            if(!Check(()=>
            {
                originalScale=fighterRoot.transform.localScale;originalPoint=fist.Limb.transform.position;
                var visual=fist.GetComponentInChildren<KiStrikeLimbVfx>(true);
                Assert(visual,"Mirrored-trail QA cannot find the native visual");
                path=visual.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(filter=>filter.sharedMesh && filter.sharedMesh.name.IndexOf("physical strike path",StringComparison.OrdinalIgnoreCase)>=0);
                Assert(path,"Mirrored-trail QA cannot find the swept physical path mesh");
            }))yield break;
            for(int facing=0;facing<2;facing++)
            {
                int sign=facing==0?1:-1;
                if(!Check(()=>
                {
                    var scale=originalScale;scale.x=Mathf.Abs(scale.x)*sign;fighterRoot.transform.localScale=scale;
                    start=fist.Limb.transform.position;ShowFistForQa(fist);
                }))yield break;
                for(int frame=0;frame<8;frame++)
                {
                    int step=frame+1;
                    if(!Check(()=>fist.Limb.transform.position=start+new Vector3(.13f,.11f,0)*step))yield break;
                    yield return null;yield return new WaitForEndOfFrame();
                }
                if(!Check(()=>
                {
                    Assert(fist.IsCharged,"Prepared fist expired during trail coordinate verification");
                    Assert(Mathf.Sign(fighterRoot.transform.lossyScale.x)==sign,"Native actor did not retain the tested facing scale");
                    var vertices=path.sharedMesh.vertices;Assert(vertices.Length>=14,"Native swept path has too few vertices");
                    float minimumWidth=float.MaxValue,maximumDot=0;
                    for(int sample=0;sample<6;sample++)
                    {
                        Vector3 a=path.transform.TransformPoint(vertices[sample*2]),b=path.transform.TransformPoint(vertices[sample*2+1]);
                        Vector3 nextA=path.transform.TransformPoint(vertices[sample*2+2]),nextB=path.transform.TransformPoint(vertices[sample*2+3]);
                        Vector3 offset=b-a,tangent=(a+b-nextA-nextB)*.5f;
                        float width=offset.magnitude,dot=Mathf.Abs(Vector3.Dot(offset.normalized,tangent.normalized));
                        Assert(tangent.magnitude>.03f,"Native diagonal trail did not record distinct physical positions");
                        minimumWidth=Mathf.Min(minimumWidth,width);maximumDot=Mathf.Max(maximumDot,dot);
                        Assert(width>.01f,"Native trail thickness collapsed under facing scale "+sign);
                        Assert(dot<.08f,"Native trail width is not perpendicular to the physical sweep under facing scale "+sign+": "+dot);
                    }
                    Capture(sign>0?"42a-fist-diagonal-trail.png":"42b-fist-mirrored-diagonal-trail.png");
                    Log("PASS native diagonal swept-path world coordinates facing="+sign+" minimumWidth="+minimumWidth+" maximumTangentDot="+maximumDot);
                    fighterRoot.transform.localScale=originalScale;fist.Limb.transform.position=originalPoint;ShowFistForQa(fist);
                }))yield break;
            }
        }

        GameObject FistVisualBackdrop(Vector3 centre)
        {
            var texture=new Texture2D(512,512,TextureFormat.RGB24,false){name="QA Unity-generated checker and contour background",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Repeat};
            var pixels=new Color32[512*512];
            for(int y=0;y<512;y++)for(int x=0;x<512;x++)
            {
                bool line=x%32<2 || y%32<2,bright=((x/32+y/32)&1)==0;
                Color32 color=line?new Color32(190,218,232,255):bright?new Color32(61,91,108,255):new Color32(28,49,66,255);
                if((x+2*y)%111<4)color=new Color32(154,116,63,255);
                if(x%128<4 || y%128<4)color=new Color32(221,233,239,255);
                pixels[y*512+x]=color;
            }
            texture.SetPixels32(pixels);texture.Apply();
            var material=new Material(Shader.Find("Sprites/Default")){name="QA only detailed unlit background",mainTexture=texture};
            var go=new GameObject("QA only background for physical refraction evidence");go.transform.position=centre;
            var mesh=new Mesh{name="QA background mesh"};
            mesh.vertices=new[]{new Vector3(-12,-6,0),new Vector3(12,-6,0),new Vector3(12,6,0),new Vector3(-12,6,0)};
            mesh.uv=new[]{Vector2.zero,new Vector2(2,0),new Vector2(2,1),Vector2.up};
            mesh.colors=new[]{Color.white,Color.white,Color.white,Color.white};mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.sortingLayerName="Background";renderer.sortingOrder=-100;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            return go;
        }

        Color32[] FistRenderCamera(Camera camera,RenderTexture target,string filename)
        {
            var previousTarget=camera.targetTexture;var previousActive=RenderTexture.active;Texture2D picture=null;
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                picture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
                picture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);picture.Apply();
                if(filename!=null)File.WriteAllBytes(Path.Combine(output,filename),picture.EncodeToPNG());
                return picture.GetPixels32();
            }
            finally{camera.targetTexture=previousTarget;RenderTexture.active=previousActive;if(picture)Destroy(picture);}
        }

        void FistVerifyBackgroundRefraction(Camera camera,KiSpaceRefraction refraction,Vector2 probePoint)
        {
            Assert(FindObjectsOfType<KiStrikeImpactVfx>().Length==0,"Emission volume contaminated the refraction-only probe");
            var target=new RenderTexture(Screen.width,Screen.height,24,RenderTextureFormat.ARGB32){name="QA same-frame camera refraction capture"};
            int requestedSamples=camera.allowMSAA?Mathf.Max(1,QualitySettings.antiAliasing):1;
            var descriptor=target.descriptor;descriptor.msaaSamples=requestedSamples;
            target.antiAliasing=SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor);
            target.Create();Texture2D difference=null;
            Log("REFRACTION CAPTURE MSAA requested="+requestedSamples+" supportedTarget="+target.antiAliasing+" / camera.allowMSAA="+camera.allowMSAA);
            try
            {
                refraction.BypassForQa=true;
                var baseline=FistRenderCamera(camera,target,"50-refraction-disabled-background.png");
                var repeated=FistRenderCamera(camera,target,null);
                refraction.BypassForQa=false;
                var distorted=FistRenderCamera(camera,target,"51-refraction-active-background.png");
                var delta=new Color32[baseline.Length];long sum=0,noiseSum=0;int changed=0,noiseChanged=0,roi=0,allChanged=0;
                double weightedX=0,weightedY=0,weightSum=0;
                Vector3 viewport=camera.WorldToViewportPoint(new Vector3(probePoint.x,probePoint.y,0));
                Vector2 expected=new Vector2(viewport.x,viewport.y),mirrored=new Vector2(viewport.x,1-viewport.y);
                float cx=target.width*expected.x,cy=target.height*expected.y,limit=target.height*.39f;
                for(int y=0;y<target.height;y++)for(int x=0;x<target.width;x++)
                {
                    int i=y*target.width+x;
                    int dr=Math.Abs(distorted[i].r-baseline[i].r),dg=Math.Abs(distorted[i].g-baseline[i].g),db=Math.Abs(distorted[i].b-baseline[i].b);
                    int noise=Math.Max(Math.Abs(repeated[i].r-baseline[i].r),Math.Max(Math.Abs(repeated[i].g-baseline[i].g),Math.Abs(repeated[i].b-baseline[i].b)));
                    int maximum=Math.Max(dr,Math.Max(dg,db));
                    if(maximum>18)
                    {
                        int weight=maximum-18;allChanged++;weightSum+=weight;
                        weightedX+=(x+.5)*weight/target.width;weightedY+=(y+.5)*weight/target.height;
                    }
                    delta[i]=new Color32((byte)Math.Min(255,dr*4),(byte)Math.Min(255,dg*4),(byte)Math.Min(255,db*4),255);
                    if((x-cx)*(x-cx)+(y-cy)*(y-cy)>limit*limit)continue;
                    roi++;sum+=dr+dg+db;noiseSum+=noise*3;
                    if(maximum>18)changed++;if(noise>18)noiseChanged++;
                }
                double mean=sum/(double)Math.Max(1,roi*3),noiseMean=noiseSum/(double)Math.Max(1,roi*3);
                var centroid=new Vector2((float)(weightedX/Math.Max(1,weightSum)),(float)(weightedY/Math.Max(1,weightSum)));
                float centreError=Vector2.Distance(centroid,expected),mirroredError=Vector2.Distance(centroid,mirrored);
                difference=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);difference.SetPixels32(delta);difference.Apply();
                File.WriteAllBytes(Path.Combine(output,"52-refraction-background-difference-x4.png"),difference.EncodeToPNG());
                string metrics="{\n  \"width\": "+target.width+",\n  \"height\": "+target.height+",\n  \"requestedMSAASamples\": "+requestedSamples+",\n  \"supportedTargetMSAASamples\": "+target.antiAliasing+",\n  \"roiPixels\": "+roi+",\n  \"changedPixelsAbove18\": "+changed+",\n  \"allChangedPixelsAbove18\": "+allChanged+",\n  \"repeatedBypassChangedPixels\": "+noiseChanged+",\n  \"meanAbsoluteRGBDifference\": "+mean.ToString("F6",System.Globalization.CultureInfo.InvariantCulture)+",\n  \"repeatedBypassMeanDifference\": "+noiseMean.ToString("F6",System.Globalization.CultureInfo.InvariantCulture)+",\n  \"expectedViewportCentre\": ["+expected.x.ToString("F6",System.Globalization.CultureInfo.InvariantCulture)+","+expected.y.ToString("F6",System.Globalization.CultureInfo.InvariantCulture)+"],\n  \"weightedDifferenceCentroid\": ["+centroid.x.ToString("F6",System.Globalization.CultureInfo.InvariantCulture)+","+centroid.y.ToString("F6",System.Globalization.CultureInfo.InvariantCulture)+"],\n  \"centreErrorViewport\": "+centreError.ToString("F6",System.Globalization.CultureInfo.InvariantCulture)+",\n  \"mirroredCentreErrorViewport\": "+mirroredError.ToString("F6",System.Globalization.CultureInfo.InvariantCulture)+",\n  \"emissionVolumes\": 0,\n  \"sameFrame\": true,\n  \"paused\": true\n}\n";
                File.WriteAllText(Path.Combine(output,"refraction-pixel-evidence.json"),metrics);
                Log("REFRACTION PIXELS changed="+changed+" baselineNoise="+noiseChanged+" mean="+mean+" baselineMean="+noiseMean);
                Log("REFRACTION POSITION expected="+expected+" centroid="+centroid+" error="+centreError+" mirroredError="+mirroredError);
                Assert(changed>Math.Max(250,noiseChanged*2+100),"No material displacement of background pixels was detected");
                Assert(mean>noiseMean*2+.05,"Refraction did not exceed same-frame rendering noise");
                Assert(centreError<.10f,"Refraction did not appear around its off-centre world position");
                Assert(centreError+.05f<mirroredError,"Refraction position is consistent with vertically flipped screen UVs");
            }
            finally{refraction.BypassForQa=false;if(difference)Destroy(difference);target.Release();Destroy(target);}
        }
    }
}
