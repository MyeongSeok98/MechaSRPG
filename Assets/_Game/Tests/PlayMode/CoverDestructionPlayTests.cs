using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace MechaXCOM.Tests
{
    public class CoverDestructionPlayTests
    {
        private BackpackTestBattle b;
        private CoverObject target;
        private PlayerInputController input;
        private GridVisualizer visual;
        private Camera camera;
        private Mouse mouse;
        private Keyboard keyboard;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditor;
        private InputSettings.BackgroundBehavior oldBackground;
        private bool changedInput;
        private void Create(bool ui = true, Vector2Int? cell = null)
        {
            b = new BackpackTestBattle();
            JsonUtility.FromJsonOverwrite("{\"rangedCategory\":2,\"minDamage\":100,\"maxDamage\":100}", b.Weapon);
            b.Combat.SetIndirectPartIndexRollSource(count=>0);
            target = DestructibleCoverTestSupport.Add(b, cell ?? new Vector2Int(4,1),100);DestructibleCoverTestSupport.Build(b);
            if(!ui)return;
            changedInput=true;oldEditor=InputSystem.settings.editorInputBehaviorInPlayMode;oldBackground=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            visual=b.Root.AddComponent<GridVisualizer>();visual.Initialize(b.Grid,b.Turns);
            var debug=b.Root.AddComponent<BattleDebugUI>();debug.Initialize(b.Grid,b.Cover);
            camera=new GameObject("Cover camera").AddComponent<Camera>();camera.transform.SetParent(b.Root.transform);
            camera.orthographic=true;camera.orthographicSize=16;camera.transform.position=new Vector3(12,26,-18);camera.transform.LookAt(new Vector3(12,0,12));
            input=b.Root.AddComponent<PlayerInputController>();input.Initialize(camera,b.Grid,visual,debug);input.InitializeCombat(b.Combat,b.Turns);
            mouse=InputSystem.AddDevice<Mouse>();keyboard=InputSystem.AddDevice<Keyboard>();
            Assert.That(input.SelectUnit(b.Player),Is.True);Assert.That(input.BeginWeaponAttack(WeaponSlot.RightHand),Is.True);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(mouse!=null)InputSystem.RemoveDevice(mouse);if(keyboard!=null)InputSystem.RemoveDevice(keyboard);
            if(changedInput){InputSystem.settings.editorInputBehaviorInPlayMode=oldEditor;InputSystem.settings.backgroundBehavior=oldBackground;}
            b?.Dispose();yield return null;
        }
        private IEnumerator Click(Vector2 point)
        {
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point}.WithButton(MouseButton.Left));yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return null;
        }
        private Component UI()
        {var type=typeof(PlayerInputController).Assembly.GetType("MechaXCOM.CoverTargetUI");Assert.That(type,Is.Not.Null);var ui=b.Root.GetComponent(type);Assert.That(ui,Is.Not.Null);return ui;}
        private CoverObject SelectedCover()=>ExplosiveTestSupport.Property<CoverObject>(input,"TargetCover");
        private bool Select()=> (bool)IndirectTestSupport.Call(input,"TrySelectCover",target);
        [UnityTest] public IEnumerator ClickCoverThenFireDestroysItAndPaysOnce()
        {
            Create();yield return null;yield return Click(camera.WorldToScreenPoint(target.transform.position));
            Assert.That(SelectedCover(),Is.SameAs(target));var ui=UI();Assert.That(ExplosiveTestSupport.Property<bool>(ui,"IsVisible"),Is.True);
            Assert.That(b.Root.GetComponent<BodyTargetUI>().IsVisible,Is.False);
            Assert.That(ExplosiveTestSupport.Property<string>(ui,"TargetInfo"),Does.Contain("100/100"));
            var fire=ExplosiveTestSupport.Property<Rect>(ui,"FireButtonRect");yield return Click(new Vector2(fire.center.x,Screen.height-fire.center.y));
            Assert.That(DestructibleCoverTestSupport.HP(target),Is.Zero);Assert.That(target.gameObject.activeInHierarchy,Is.False);
            Assert.That(b.Player.ActionPoints.CurrentPoints,Is.EqualTo(2));Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.RightHand).Loaded,Is.EqualTo(5));
            Assert.That(SelectedCover(),Is.Null);Assert.That(input.InputMessage,Does.Contain("파괴"));
        }
        [UnityTest] public IEnumerator WeaponSwitchAndEscapeClearCoverAimWithoutResources()
        {
            Create();Assert.That(Select(),Is.True);b.Player.Combat.ConfigureTestMelee(8,95,2,3);
            Assert.That(input.BeginWeaponAttack(WeaponSlot.LeftHand),Is.True);Assert.That(SelectedCover(),Is.Null);
            Assert.That(input.BeginWeaponAttack(WeaponSlot.RightHand),Is.True);Assert.That(Select(),Is.True);yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            Assert.That(input.SelectedUnit,Is.Null);Assert.That(SelectedCover(),Is.Null);Assert.That(b.Player.ActionPoints.CurrentPoints,Is.EqualTo(5));
        }
        [UnityTest] public IEnumerator DestructionRefreshesVisibleMoveRangeWithoutChangingAP()
        {
            Create();Assert.That(input.BeginMove(),Is.True);yield return null;
            var overlay=visual.transform.Find("Reachable Cells");int before=overlay.childCount;
            DestructibleCoverTestSupport.Health(target).GetType().GetMethod("ApplyDamage").Invoke(DestructibleCoverTestSupport.Health(target),new object[]{100});
            yield return null;Assert.That(overlay.childCount,Is.GreaterThan(before));Assert.That(b.Player.ActionPoints.CurrentPoints,Is.EqualTo(5));
            Assert.That(Pathfinder.FindPath(b.Grid,b.Player,new Vector2Int(4,1)),Is.Not.Empty);
        }
        [UnityTest] public IEnumerator IndirectFireAlsoDestroysCoverAndReportsCoverDamage()
        {
            Create(cell:new Vector2Int(3,2));IndirectTestSupport.Configure(b,2);
            Assert.That(input.BeginWeaponAttack(WeaponSlot.RightHand),Is.True);Assert.That(input.TrySelectImpactCell(new Vector2Int(3,1)),Is.True);yield return null;
            var result=input.TryFireIndirectSelected();Assert.That(result.WasExecuted,Is.True);
            Assert.That(DestructibleCoverTestSupport.HP(target),Is.Zero);
            var results=IndirectTestSupport.Field<System.Collections.Generic.IReadOnlyList<CoverAttackResult>>(result,"Covers");
            Assert.That(results.Count,Is.EqualTo(1));Assert.That(results[0].WasDestroyed,Is.True);
        }
        [UnityTest] public IEnumerator ExistingIndirectAIShotDamagesCoverInItsBlast()
        {
            Create(false,new Vector2Int(2,1));IndirectTestSupport.Configure(b,1);
            var ai=b.Root.AddComponent<EnemyAIController>();ai.Initialize(b.Grid,b.Combat,b.Turns);b.Turns.ConfigureEnemyAI(ai);
            Assert.That(b.Turns.TryEndTurn(),Is.True);float end=Time.realtimeSinceStartup+6;
            while(!b.Turns.IsPlayerTurn&&Time.realtimeSinceStartup<end)yield return null;
            Assert.That(b.Turns.IsPlayerTurn,Is.True);Assert.That(DestructibleCoverTestSupport.HP(target),Is.Zero);
            Assert.That(b.Player.Health.GetCurrentHP(TargetBodyPart.Torso),Is.EqualTo(900));
        }
        [UnityTest] public IEnumerator UnregisteredCoverClearsAimWithoutSpendingResources()=>LostTarget(false);
        [UnityTest] public IEnumerator DisabledDurabilityClearsAimWithoutSpendingResources()=>LostTarget(true);
        private IEnumerator LostTarget(bool disabled)
        {
            Create();Assert.That(Select(),Is.True);
            if(disabled)((Behaviour)DestructibleCoverTestSupport.Health(target)).enabled=false;
            else b.Cover.Build(b.Grid,System.Array.Empty<CoverObject>());
            yield return null;
            Assert.That(SelectedCover(),Is.Null);Assert.That(ExplosiveTestSupport.Property<bool>(UI(),"IsVisible"),Is.False);
            Assert.That(input.TryFireCoverSelected().WasExecuted,Is.False);
            Assert.That(b.Player.ActionPoints.CurrentPoints,Is.EqualTo(5));Assert.That(b.Player.Ammo.GetStatus(WeaponSlot.RightHand).Loaded,Is.EqualTo(6));
        }
        [UnityTest] public IEnumerator BlastOverlayIncludesStruckWallFaceButNotCellsBehindIt()
        {
            Create(cell:new Vector2Int(3,2));target.gameObject.AddComponent<GridObstacle>();DestructibleCoverTestSupport.Build(b);
            IndirectTestSupport.Configure(b,2);Assert.That(input.BeginWeaponAttack(WeaponSlot.RightHand),Is.True);
            Assert.That(input.TrySelectImpactCell(new Vector2Int(3,1)),Is.True);yield return null;
            var overlay=visual.transform.Find("Blast Preview Cells");Assert.That(overlay,Is.Not.Null);
            bool face=false,behind=false;
            foreach(Transform item in overlay)
                if(b.Grid.TryWorldToGrid(item.position,out var position))
                {if(position==new Vector2Int(3,2))face=true;if(position==new Vector2Int(3,3))behind=true;}
            Assert.That(face,Is.True);Assert.That(behind,Is.False);
        }
    }
}
