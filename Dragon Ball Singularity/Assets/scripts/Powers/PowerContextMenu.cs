using UnityEngine;

namespace DBPU
{
    // Adds "Powers Menu" to a fighter's context menu. Only one powers window is open at a time.
    public class PowerContextMenu : MonoBehaviour
    {
        public PersonBehaviour person;
        public CharacterPowers PowersData;

        public int PositionSelectedFrame = 0;
        public int PowerSelected = 0;

        static PowerMenuWindow open;
        static PowerContextMenu openOwner;

        public static PowerMenuWindow OpenWindow { get { return open != null && open.Window ? open : null; } }

        public void Awake()
        {
            var contextButtons = gameObject.GetComponent<PhysicalBehaviour>().ContextMenuOptions;
            MCP_Utils.AddContextButtonInOrder(contextButtons, new ContextMenuButton("!PowersMenu", "Powers Menu", "Powers Menu", () => OpenPowersMenu()), 0);
        }

        public static PowerContextMenu GetForSelectedLimb()
        {
            var selection = SelectionController.Main;
            if (selection != null && selection.SelectedObjects != null)
            {
                foreach (PhysicalBehaviour selected in selection.SelectedObjects)
                {
                    var menu = FindPowerMenuFromPhysical(selected);
                    if (menu != null) return menu;
                }
            }

            var tool = Global.main?.ToolControllerBehaviour?.CurrentTool;
            if (tool != null && tool.ActiveSingleSelected != null)
            {
                var menu = FindPowerMenuFromPhysical(tool.ActiveSingleSelected);
                if (menu != null) return menu;
            }

            if (selection != null && selection.CurrentlyUnderMouse != null)
                return FindPowerMenuFromPhysical(selection.CurrentlyUnderMouse);

            return null;
        }

        static PowerContextMenu FindPowerMenuFromPhysical(PhysicalBehaviour pb)
        {
            if (pb == null) return null;

            var limb = pb.GetComponent<LimbBehaviour>();
            if (limb != null && limb.Person != null && limb.Person.Limbs != null && limb.Person.Limbs.Length > 0)
                return limb.Person.Limbs[0].GetComponent<PowerContextMenu>();

            var person = pb.GetComponent<PersonBehaviour>() ?? pb.transform.root.GetComponent<PersonBehaviour>();
            if (person == null || person.Limbs == null || person.Limbs.Length == 0) return null;

            for (int i = 0; i < person.Limbs.Length; i++)
            {
                if (person.Limbs[i] != null && person.Limbs[i].gameObject == pb.gameObject)
                    return person.Limbs[0].GetComponent<PowerContextMenu>();
            }

            return null;
        }

        public void OpenPowersMenu()
        {
            if (PowersData == null) return;
            if (OpenWindow != null)
            {
                if (openOwner == this) return;
                open.Window.CloseImmediate();
                open = null;
            }

            PowerSetup.RegisterUniversalPhysicalPowers(PowersData, person);
            PowersData.HarvestPowerStates();
            var powerList = PowersData.GetOrderedPowers();
            if (powerList.Count == 0) return;

            int index = Mathf.Clamp(PowerSelected, 0, powerList.Count - 1);
            openOwner = this;
            open = new PowerMenuWindow(this, PowersData, person, powerList[index]);
        }

        internal void RememberSelection(int index)
        {
            if (index < 0) return;
            PowerSelected = index;
            PositionSelectedFrame = index;
        }

        internal void HandleWindowClosed()
        {
            if (openOwner != this) return;
            open = null;
            openOwner = null;
        }
    }
}
