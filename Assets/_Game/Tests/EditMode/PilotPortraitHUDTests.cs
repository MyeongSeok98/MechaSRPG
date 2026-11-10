using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MechaXCOM.Tests
{
    public class PilotPortraitHUDTests
    {
        private BackpackTestBattle b;
        private BattleDebugUI hud;
        private CharacterData pilot;
        private Texture2D texture;
        private Sprite portrait;
        [SetUp] public void Setup()
        {
            b = new BackpackTestBattle(); hud = b.Root.AddComponent<BattleDebugUI>(); hud.Initialize(b.Grid, b.Cover);
            hud.InitializeCombat(null, b.Turns);
            texture = new Texture2D(4, 5); portrait = Sprite.Create(texture, new Rect(0, 0, 4, 5), Vector2.one * .5f);
            pilot = ScriptableObject.CreateInstance<CharacterData>();
            typeof(CharacterData).GetField("defaultPortrait", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pilot, portrait);
        }
        [TearDown] public void Cleanup()
        { b.Dispose(); Object.DestroyImmediate(pilot); Object.DestroyImmediate(portrait); Object.DestroyImmediate(texture); }
        private Sprite Shown()
        {
            var property = typeof(BattleDebugUI).GetProperty("PilotPortrait");
            Assert.That(property, Is.Not.Null, "HUD must expose the selected pilot's Default portrait instead of a model preview.");
            return (Sprite)property.GetValue(hud);
        }
        private void Fallback()
        {
            var field = typeof(BattleDebugUI).GetField("fallbackPortrait", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null); field.SetValue(hud, portrait);
        }
        private void Pilot(BattleUnit unit, CharacterData value) =>
            typeof(BattleUnit).GetField("pilotData", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(unit, value);
        [Test] public void SelectedUnitShowsItsPilotsDefaultPortrait()
        { Pilot(b.Player, pilot); hud.SetSelectedUnit(b.Player); Assert.That(Shown(), Is.SameAs(portrait)); }
        [Test] public void NoPilotUsesConfiguredSamplePortrait()
        { Fallback(); hud.SetSelectedUnit(b.Player); Assert.That(Shown(), Is.SameAs(portrait)); }
        [Test] public void MissingDefaultUsesConfiguredSamplePortrait()
        {
            Fallback(); typeof(CharacterData).GetField("defaultPortrait", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pilot, null);
            Pilot(b.Player, pilot); hud.SetSelectedUnit(b.Player); Assert.That(Shown(), Is.SameAs(portrait));
        }
        [Test] public void SelectionChangeDoesNotRetainPreviousPilotsPortrait()
        {
            Pilot(b.Player, pilot); hud.SetSelectedUnit(b.Player); Assert.That(Shown(), Is.SameAs(portrait));
            hud.SetSelectedUnit(b.Enemy); Assert.That(Shown(), Is.Null);
        }
        [Test] public void ClearedAndDisabledHUDNeverShowPortrait()
        {
            Fallback(); hud.SetSelectedUnit(b.Player); Assert.That(Shown(), Is.Not.Null);
            hud.SetSelectedUnit(null); Assert.That(Shown(), Is.Null);
            hud.SetSelectedUnit(b.Player); hud.enabled = false; Assert.That(Shown(), Is.Null);
        }
        [Test] public void HiddenEnemyPortraitDisappearsImmediately()
        {
            Pilot(b.Enemy, pilot); hud.SetSelectedUnit(b.Enemy); Assert.That(Shown(), Is.SameAs(portrait));
            b.Player.Vision.Configure(0); b.Turns.Visibility.Refresh(); Assert.That(Shown(), Is.Null);
        }
    }
}
