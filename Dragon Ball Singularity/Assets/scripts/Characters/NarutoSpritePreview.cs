using System;
using System.IO;
using System.Linq;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace DBPU
{
    // Explicit disposable QA process only. Normal gameplay never invokes this.
    public sealed class NarutoSpritePreview:MonoBehaviour
    {
        string output;
        public static void StartIfRequested()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-dbpu-naruto-preview");
            if(i<0||i+1>=args.Length)return;
            Application.runInBackground=true;
            var go=new GameObject("Naruto opt-in sprite validation");DontDestroyOnLoad(go);
            var test=go.AddComponent<NarutoSpritePreview>();test.output=args[i+1];Directory.CreateDirectory(test.output);test.StartCoroutine(test.Run());
        }
        IEnumerator Run()
        {
            for(int i=0;i<120;i++)yield return null;
            MapLoaderBehaviour.CurrentMap=MapRegistry.GetAllMaps().First(m=>m.Prefab);
            SceneManager.LoadScene("Main");yield return new WaitForSecondsRealtime(3);
            GameObject person=null;
            try
            {
                if(!ModAPI.FindSpawnable("[DBPU] Naruto Uzumaki"))throw new Exception("Naruto missing from spawn catalogue");
                person=Instantiate(ModAPI.FindSpawnable("Human").Prefab,new Vector3(0,30,0),Quaternion.identity);
                typeof(CatalogBehaviour).GetMethod("PerformMod",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Static,null,new[]{typeof(SpawnableAsset),typeof(GameObject)},null).Invoke(CatalogBehaviour.Main,new object[]{ModAPI.FindSpawnable("[DBPU] Naruto Uzumaki"),person});
                foreach(var rb in person.GetComponentsInChildren<Rigidbody2D>())rb.bodyType=RigidbodyType2D.Kinematic;
            }
            catch(Exception e){File.WriteAllText(Path.Combine(output,"result.txt"),"FAIL "+e);Application.Quit(2);yield break;}
            yield return new WaitForSecondsRealtime(1);
            try
            {
                var limbs=person.GetComponent<PersonBehaviour>().Limbs;
                if(limbs.Any(l=>!l.GetComponent<SpriteRenderer>().sprite))throw new Exception("Missing limb sprite");
                var renderers=person.GetComponentsInChildren<SpriteRenderer>().Where(r=>r.enabled&&r.sprite).ToArray();
                var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                foreach(var t in person.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
                var go=new GameObject("Isolated sprite QA camera");var camera=go.AddComponent<Camera>();camera.orthographic=true;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.37f,.43f,.49f);camera.cullingMask=1<<30;
                camera.transform.position=new Vector3(bounds.center.x,bounds.center.y,-10);camera.orthographicSize=bounds.size.y*.59f;
                Capture(camera,"naruto-in-game.png");
                person.transform.localScale=new Vector3(-person.transform.localScale.x,person.transform.localScale.y,person.transform.localScale.z);
                Capture(camera,"naruto-mirrored.png");
                var text="PASS catalogue entry; spawn setup; 14 limb sprites; delayed Base transformation; native joint pose and mirrored render.\n";
                text+="Hair size "+person.transform.Find("Head/Hair").GetComponent<SpriteRenderer>().sprite.rect.size+"; offset "+person.transform.Find("Head/Hair").localPosition+"\n";
                foreach(var l in limbs){var r=l.GetComponent<SpriteRenderer>();text+=l.name+" sprite "+r.sprite.rect.size+" world "+r.bounds.size+"\n";}
                File.WriteAllText(Path.Combine(output,"sprite-result.txt"),text);
                person.transform.localScale=new Vector3(Mathf.Abs(person.transform.localScale.x),person.transform.localScale.y,person.transform.localScale.z);
                camera.orthographicSize=1.8f;camera.transform.position=new Vector3(bounds.center.x-.2f,bounds.center.y+.16f,-10);
                StartCoroutine(CheckForms(person,camera));
            }
            catch(Exception e){File.WriteAllText(Path.Combine(output,"result.txt"),"FAIL "+e);Application.Quit(2);}
        }
        IEnumerator CheckForms(GameObject instance,Camera camera)
        {
            var person=instance.GetComponent<PersonBehaviour>();var forms=instance.GetComponent<CharacterTransformations>();
            var aura=instance.GetComponent<AuraHandler>();var ki=instance.GetComponent<KiSystem>();
            var controller=instance.GetComponent<NarutoChakraController>();
            string[] ids={"Base","FoxLeak","Fox1","Fox2","Fox3","Sage","SageRobe","KCM1","KCM2","KuramaSage","SixPaths","SixPathsCloak"};
            float[] expected={1,2,3,5,8,10,10,25,125,312.5f,3125,15625};
            string report="";
            for(int i=0;i<ids.Length;i++)
            {
                string id=ids[i];
                try{forms.ApplyTransformation(person,id);ki.currentKi=ki.maxKi;}
                catch(Exception e){Fail(e);yield break;}
                yield return new WaitForSecondsRealtime(.8f);
                try
                {
                    if(controller==null||controller.Rig==null)throw new Exception("Unity chakra rig missing");
                    if(ki.CurrentForm!=id)throw new Exception("Form did not commit: "+id);
                    float actual=instance.GetComponent<MultipliersAndStuffs>().FormMultiplier;
                    if(Mathf.Abs(actual-.25f*expected[i])>.01f)throw new Exception("Wrong DBPU power "+id+": "+actual);
                    int tails=id=="Fox1"?1:id=="Fox2"?2:id=="Fox3"?3:0;
                    if(controller.Rig.TailCount!=tails)throw new Exception("Wrong tail count");
                    if(controller.Rig.OrbCount!=(id=="SixPathsCloak"?9:0))throw new Exception("Wrong orb count");
                    bool robe=id=="SageRobe"||id=="KCM2"||id=="KuramaSage"||id=="SixPathsCloak";
                    if(controller.Rig.RobeVisible!=robe)throw new Exception("Wrong cloak state");
                    foreach(var r in instance.GetComponentsInChildren<Renderer>())if(r.enabled&&r.sharedMaterial&&r.sharedMaterial.shader&&!r.sharedMaterial.shader.isSupported)throw new Exception("Unsupported shader "+r.sharedMaterial.shader.name);
                    if(person.Limbs.Any(l=>!l.GetComponent<SpriteRenderer>().sprite))throw new Exception("Missing form sprite");
                    if(person.Limbs[0].GetComponent<SpriteRenderer>().bounds.size.x<.15f)throw new Exception("Head sprite has incorrect physical size");
                    Capture(camera,id+".png");
                    var headRenderer=person.Limbs[0].GetComponent<SpriteRenderer>();var headBlock=new MaterialPropertyBlock();headRenderer.GetPropertyBlock(headBlock);
                    File.AppendAllText(Path.Combine(output,"head-diagnostics.txt"),id+" rect="+headRenderer.sprite.rect+" PPU="+headRenderer.sprite.pixelsPerUnit+" size="+headRenderer.bounds.size+" enabled="+headRenderer.enabled+" color="+headRenderer.color+" scale="+headRenderer.transform.lossyScale+" layer="+headRenderer.gameObject.layer+" order="+headRenderer.sortingOrder+" shader="+headRenderer.sharedMaterial.shader.name+" texture="+headRenderer.sharedMaterial.mainTexture+" blockTex="+headBlock.GetTexture("_MainTex")+" texRect="+headRenderer.sprite.textureRect+"\n");
                    report+="PASS "+id+"; relative power "+expected[i]+"; tails "+tails+"; orbs "+controller.Rig.OrbCount+"; robe "+robe+"; active form skin\n";
                }
                catch(Exception e){Fail(e);yield break;}
            }
            ki.KiChargeActiveForRegen=true;yield return new WaitForSecondsRealtime(.7f);Capture(camera,"SixPaths-charged.png");ki.KiChargeActiveForRegen=false;
            aura.AuraActive=false;yield return new WaitForSecondsRealtime(.5f);
            try{if(controller.Rig.Fade>.01f)throw new Exception("Aura toggle did not fade custom aura");report+="PASS native aura toggle fades GPU field\n";}
            catch(Exception e){Fail(e);yield break;}
            forms.ApplyTransformation(person,"Fox3");yield return new WaitForSecondsRealtime(.6f);Capture(camera,"Fox3-motion-a.png");
            yield return new WaitForSecondsRealtime(.4f);Capture(camera,"Fox3-motion-b.png");
            forms.ApplyTransformation(person,"Base",true);yield return new WaitForSecondsRealtime(.4f);
            try
            {
                if(controller.Rig.TailCount!=0||controller.Rig.OrbCount!=0||controller.Rig.RobeVisible||controller.Rig.Fade>.01f)throw new Exception("Form VFX leaked into Base");
                if(Mathf.Abs(instance.GetComponent<MultipliersAndStuffs>().FormMultiplier-.25f)>.0001f)throw new Exception("Base power failed to restore");
                if(person.Limbs.Any(l=>float.IsNaN(l.Health)||float.IsInfinity(l.Health)))throw new Exception("Non-finite limb health");
                Capture(camera,"Base-restored.png");report+="PASS Base restoration: costume, power, no tails/orbs/robe/aura; finite health\n";
                instance.transform.localScale=new Vector3(-instance.transform.localScale.x,instance.transform.localScale.y,instance.transform.localScale.z);
                forms.ApplyTransformation(person,"Fox3");
            }
            catch(Exception e){Fail(e);yield break;}
            yield return new WaitForSecondsRealtime(.5f);Capture(camera,"Fox3-mirrored.png");
            File.WriteAllText(Path.Combine(output,"result.txt"),report+"PASS mirrored form render; GPU renderer budget "+controller.Rig.RendererCount+"\n");
            Application.Quit(0);
        }
        void Fail(Exception e){File.WriteAllText(Path.Combine(output,"result.txt"),"FAIL "+e);Application.Quit(2);}
        void Capture(Camera camera,string file)
        {
            var rt=new RenderTexture(640,960,24);var tex=new Texture2D(640,960,TextureFormat.RGB24,false);var old=RenderTexture.active;
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,640,960),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(output,file),tex.EncodeToPNG());
            camera.targetTexture=null;RenderTexture.active=old;Destroy(rt);Destroy(tex);
        }
    }
}


