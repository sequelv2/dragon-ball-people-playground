using System.Text;
using UnityEngine;

namespace DBPU
{
    // Telemetry for up to three AI fighters, top left of the screen. Switched from a fighter's head
    // menu ("AI telemetry"). The text is rebuilt four times a second into one reused builder; nothing
    // exists at all while the overlay is off.
    public sealed class AutonomousDebugOverlay : MonoBehaviour
    {
        static AutonomousDebugOverlay instance;
        readonly StringBuilder text = new StringBuilder(2048);
        readonly GUIContent content = new GUIContent();
        GUIStyle style; Texture2D backdrop; float clock;

        public static void Tick()
        {
            bool wanted = AutonomousConfig.Overlay && SingularityMind.First != null;
            if (wanted && !instance)
            {
                var go = new GameObject("DBPU Singularity Mind overlay");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<AutonomousDebugOverlay>();
            }
            else if (!wanted && instance) { Destroy(instance.gameObject); instance = null; }
        }

        void Update()
        {
            clock -= Time.unscaledDeltaTime;
            if (clock > 0f) return;
            clock = .25f;
            text.Length = 0;
            int shown = 0;
            for (int i = 0; i < SingularityMind.All.Count && shown < 3; i++)
            {
                var mind = SingularityMind.All[i];
                if (!mind || !mind.AiOn) continue;
                Describe(mind);
                shown++;
            }
            content.text = text.ToString();
        }

        void Describe(SingularityMind mind)
        {
            var strikes = mind.Strikes; var abilities = mind.Abilities; var sensor = mind.Sensor;
            text.Append("<b>").Append(SingularityFighters.DisplayName(mind.Person)).Append("</b>  ").Append(mind.Difficulty).Append(" / ").Append(mind.Stance).Append('\n');
            text.Append("tactic ").Append(AiTactic.Names[Mathf.Clamp(mind.Tactic, 0, AiTactic.Count - 1)]);
            text.Append("   target ").Append(mind.HasTarget ? SingularityFighters.DisplayName(mind.Foe.Person) : "none").Append('\n');
            text.Append("stamina ").Append(mind.Stamina.ToString("0.00")).Append("  ki ").Append(abilities.KiFraction.ToString("0.00"));
            text.Append("  tier ").Append(mind.Tier01.ToString("0.00")).Append("  pressure ").Append(mind.Pressure.ToString("0.00"));
            text.Append("  stop risk ").Append(mind.StopRisk.ToString("0.00")).Append("  reaction ").Append((mind.ReactionDelay * 1000f).ToString("0")).Append(" ms\n");
            text.Append("strikes ").Append(strikes.RecordCount).Append("  hit ").Append(strikes.HitRate.ToString("0.00")).Append("  stopped ").Append(strikes.StopRate.ToString("0.00"));
            text.Append("  whiff ").Append(strikes.WhiffRate.ToString("0.00")).Append("  gain ").Append(strikes.Gain.ToString("0.00"));
            text.Append("  bias +").Append(strikes.SpeedBias.ToString("0.0")).Append("  kept ").Append(strikes.GateRatio.ToString("0.00")).Append('\n');
            if (strikes.RecordCount > 0)
            {
                var last = strikes.Records[(strikes.RecordCount - 1) % strikes.Records.Length];
                text.Append("last ").Append(last.Kick ? "kick" : "punch").Append(": asked ").Append(last.Commanded.ToString("0.0")).Append("  peak ").Append(last.Peak.ToString("0.0"));
                text.Append("  read ").Append(last.GateSpeed.ToString("0.0")).Append(" m/s  ").Append(last.Result).Append("  joint ").Append(last.JointLoad.ToString("0.00")).Append('\n');
            }
            var threat = sensor.TopThreat;
            text.Append("threat ").Append(sensor.ThreatLevel.ToString("0.00"));
            if (threat != null) text.Append("  ").Append(threat.Kind).Append(" in ").Append(threat.Tti.ToString("0.00")).Append(" s");
            text.Append("  ground ").Append(sensor.GroundDistance.ToString("0.0")).Append(" m  ledger ").Append(mind.Ledger.Count).Append('\n');
            text.Append("plan ");
            var plan = mind.BestPlan;
            for (int i = 0; i < plan.Length; i++) text.Append(i == 0 ? "" : " > ").Append(AiTactic.Names[Mathf.Clamp(plan[i], 0, AiTactic.Count - 1)]);
            text.Append('\n');
            var memory = mind.Memory;
            if (memory != null) text.Append("memory vs ").Append(memory.OpponentId).Append(": ").Append(memory.Updates).Append(" lessons, ").Append(memory.Wins).Append(" won, ").Append(memory.Losses).Append(" lost\n");
            text.Append('\n');
        }

        void OnGUI()
        {
            if (string.IsNullOrEmpty(content.text)) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true, wordWrap = false, alignment = TextAnchor.UpperLeft };
                style.normal.textColor = new Color(.92f, .95f, 1f, 1f);
                backdrop = new Texture2D(1, 1);
                backdrop.SetPixel(0, 0, new Color(0f, 0f, 0f, .55f)); backdrop.Apply();
            }
            Vector2 size = style.CalcSize(content);
            var rect = new Rect(12f, 110f, size.x + 16f, size.y + 10f);
            GUI.DrawTexture(rect, backdrop);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 5f, size.x, size.y), content, style);
        }

        void OnDestroy() { if (backdrop) Destroy(backdrop); if (instance == this) instance = null; }
    }
}
