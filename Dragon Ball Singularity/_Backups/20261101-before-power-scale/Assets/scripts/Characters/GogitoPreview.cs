using System;
using System.IO;
using System.Linq;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DBPU
{
    // Runs only in an explicitly launched, disposable QA game process.
    public sealed class GogitoPreview:MonoBehaviour
    {
        string output; Camera camera; GameObject subject;
        public static void StartIfRequested()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-dbpu-gogito-preview");
            if(i<0||i+1>=args.Length)return;
            Application.runInBackground=true;
            var go=new GameObject("Gogito opt-in QA");DontDestroyOnLoad(go);
            var test=go.AddComponent<GogitoPreview>();test.output=args[i+1];Directory.CreateDirectory(test.output);test.StartCoroutine(test.Run());
        }
        void Log(string message){File.AppendAllText(Path.Combine(output,"result.txt"),message+"\n");}
        bool Check(Action action){try{action();return true;}catch(Exception ex){Log("FAIL "+ex);Application.Quit(2);return false;}}
        void Assert(bool ok,string message){if(!ok)throw new Exception(message);}
        GameObject Spawn(string name,Vector3 pos)
        {
            var asset=ModAPI.FindSpawnable(name);Assert(asset,"Missing catalogue entry "+name);
            var root=Instantiate(ModAPI.FindSpawnable("Human").Prefab,pos,Quaternion.identity);
            typeof(CatalogBehaviour).GetMethod("PerformMod",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Static,null,new[]{typeof(SpawnableAsset),typeof(GameObject)},null)
                .Invoke(CatalogBehaviour.Main,new object[]{asset,root});
            foreach(var rb in root.GetComponentsInChildren<Rigidbody2D>())rb.bodyType=RigidbodyType2D.Kinematic;
            return root;
        }
        IEnumerator Run()
        {
            File.WriteAllText(Path.Combine(output,"result.txt"),"Gogito real-game QA started\n");
            for(int i=0;i<120;i++)yield return null;
            MapLoaderBehaviour.CurrentMap=MapRegistry.GetAllMaps().First(m=>m.Prefab);
            SceneManager.LoadScene("Main");yield return new WaitForSecondsRealtime(3);
            GameObject gogeta=null;
            if(!Check(()=>{subject=Spawn("[DBPU] Gogito",new Vector3(0,30,0));gogeta=Spawn("[DBPU] (BM) Gogeta",new Vector3(8,30,0));}))yield break;
            yield return new WaitForSecondsRealtime(1.5f);
            var person=subject.GetComponent<PersonBehaviour>();var ki=subject.GetComponent<KiSystem>();
            var menu=subject.transform.Find("Head").GetComponent<TransformationContextMenu>();
            var forms=subject.GetComponent<CharacterTransformations>();var aura=subject.GetComponent<AuraHandler>();
            var kai=subject.GetComponent<Kaioken>();var mult=subject.GetComponent<MultipliersAndStuffs>();
            var gp=gogeta.GetComponent<PersonBehaviour>();var gm=gogeta.transform.Find("Head").GetComponent<TransformationContextMenu>();
            if(!Check(()=>gm.ApplyForm("SSJBlue")))yield break;
            yield return new WaitForSecondsRealtime(1.2f);
            float basePower=mult.FormMultiplier;float baseHealth=person.Limbs[0].InitialHealth;
            if(!Check(()=>{
                float bluePower=gogeta.GetComponent<MultipliersAndStuffs>().FormMultiplier;
                Assert(basePower>bluePower,"Base Gogito must exceed Gogeta Blue");
                Assert(person.Limbs[0].InitialHealth>gp.Limbs[0].InitialHealth,"Base durability must exceed Gogeta Blue");
                Assert(subject.transform.Find("Head/Potara")&&subject.transform.Find("Head/PotaraFar"),"Missing earrings");
                Assert(forms.transformations.TransformationData.Count==6,"Expected 6 forms");
                Assert(subject.GetComponent<BlastBehaviour>()&&subject.GetComponent<BeamBehaviour>()&&subject.GetComponent<ZenkaiBoostBehaviour>(),"Native combat components missing");
                Log("PASS native catalogue, two Potara, combat; Gogito base="+basePower+" > Gogeta Blue="+bluePower+"; health="+baseHealth+" > "+gp.Limbs[0].InitialHealth);
                camera=new GameObject("Gogito isolated QA camera").AddComponent<Camera>();camera.orthographic=true;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.29f,.34f,.40f);camera.cullingMask=1<<30;
                var bounds=person.Limbs[0].GetComponent<SpriteRenderer>().bounds;
                foreach(var l in person.Limbs)bounds.Encapsulate(l.GetComponent<SpriteRenderer>().bounds);
                camera.transform.position=new Vector3(bounds.center.x-.13f,bounds.center.y+.13f,-10);camera.orthographicSize=1.65f;
                Capture("Base.png",false);
            }))yield break;
            string[] ids={"Base","SSJ1","SSJ2","SSJ3","SSJGod","SSJBlue"};float[] ratios={1,50,100,400,10000,500000};
            for(int i=0;i<ids.Length;i++)
            {
                string id=ids[i];float ratio=ratios[i];
                if(!Check(()=>menu.ApplyForm(id)))yield break;
                yield return new WaitForSecondsRealtime(1.2f);
                ki.currentKi=ki.maxKi;ki.Strain=0;
                if(!Check(()=>{
                    Assert(ki.CurrentForm==id,"Wrong active form "+id+" / "+ki.CurrentForm);
                    Assert(Math.Abs(mult.FormMultiplier/(basePower*ratio)-1)<.001,"Wrong strength "+id);
                    Assert(person.Limbs.All(l=>l.GetComponent<SpriteRenderer>().sprite && !float.IsNaN(l.Health)&&!float.IsInfinity(l.InitialHealth)),"Invalid sprite/health "+id);
                    Assert(person.Limbs[0].GetComponent<SpriteRenderer>().bounds.size.x>.15f,"Wrong head scale");
                    Capture(id+"-sprite.png",false);
                    aura.AuraActive=true;aura.AuraVisible=true;aura.CreateAura(id);
                    Log("PASS "+id+"; power="+mult.FormMultiplier+"; HP="+person.Limbs[0].InitialHealth+"; hair="+subject.transform.Find("Head/Hair").localPosition);
                }))yield break;
                yield return new WaitForSecondsRealtime(.5f);
                if(!Check(()=>Capture(id+"-aura.png",true)))yield break;
                var hair=subject.transform.Find("Head/Hair");Vector3 hairScale=hair.localScale;Vector3 hairPos=hair.localPosition;
                if(!Check(()=>{kai.ApplyConfiguredMultiplier(20);kai.ToggleFromPowersMenu();}))yield break;
                yield return new WaitForSecondsRealtime(.8f);
                if(!Check(()=>{
                    Assert(kai.Active,"Kaioken failed on "+id);
                    Assert(Math.Abs(mult.FinalMultiplier/(mult.FormMultiplier*20)-1)<.02,"Kaioken multiplier not applied");
                    Assert(hair.localScale==hairScale&&hair.localPosition==hairPos,"Kaioken changed hair geometry");
                    Assert(person.Limbs.All(l=>!float.IsInfinity(l.InitialHealth)&&!float.IsNaN(l.InitialHealth)),"Kaioken overflow");
                    Capture(id+"-Kaioken.png",true);Log("PASS "+id+" + Kaioken x20, finite health and unchanged hair");
                    kai.ToggleFromPowersMenu();
                }))yield break;
                yield return new WaitForSecondsRealtime(.4f);
                aura.AuraActive=false;aura.DestroyAura();
            }
            if(!Check(()=>menu.ApplyForm("Base")))yield break;
            yield return new WaitForSecondsRealtime(1.2f);
            if(!Check(()=>{
                Assert(Math.Abs(person.Limbs[0].InitialHealth/baseHealth-1)<.01,"Health did not restore after repeated Blue/Kaioken");
                Assert(Math.Abs(mult.FormMultiplier/basePower-1)<.001&&!kai.Active,"Base power did not restore");
                subject.transform.localScale=new Vector3(-subject.transform.localScale.x,subject.transform.localScale.y,subject.transform.localScale.z);
                Capture("Base-mirrored.png",false);Log("PASS Base health/power restoration; mirrored render");
                // Numerical cap must preserve wounds when traversing the entire power range.
                var limb=person.Limbs[5];limb.Health=limb.InitialHealth*.5f;
                GogitoVitality.Scale(limb,10000000);GogitoVitality.Scale(limb,.0000001f);
                Assert(Math.Abs(limb.Health/limb.InitialHealth-.5f)<.001,"Health cap healed an injury");
                Log("PASS finite cap roundtrip preserves injury fraction\nALL CHECKS PASSED");
            }))yield break;
            Application.Quit(0);
        }
        void Capture(string file,bool vfx)
        {
            foreach(var t in subject.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
            var handler=subject.GetComponent<AuraHandler>();if(handler.CurrentAuraObject)
                foreach(var t in handler.CurrentAuraObject.GetComponentsInChildren<Transform>(true))t.gameObject.layer=vfx?30:29;
            foreach(var effect in FindObjectsOfType<RefinedAuraVisual>())if(effect.KaiokenPerson==subject.GetComponent<PersonBehaviour>())
                foreach(var t in effect.GetComponentsInChildren<Transform>(true))t.gameObject.layer=vfx?30:29;
            var rt=new RenderTexture(640,960,24);var tex=new Texture2D(640,960,TextureFormat.RGB24,false);var old=RenderTexture.active;
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,640,960),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(output,file),tex.EncodeToPNG());
            camera.targetTexture=null;RenderTexture.active=old;Destroy(rt);Destroy(tex);
        }
    }
}
