using System;
using UnityEngine;

namespace MechaXCOM
{
    public partial class TurnManager
    {
        public event Action<UnitTeam> TurnStarted;
        private UnityEngine.Object turnPresentationOwner;
        private float turnPresentationUntil;
        public bool IsPresentingTurn => Application.isPlaying && isActiveAndEnabled && !IsBattleOver &&
            turnPresentationOwner && Time.unscaledTime < turnPresentationUntil;
        internal bool OwnsTurnPresentation(UnityEngine.Object owner) => IsPresentingTurn && ReferenceEquals(turnPresentationOwner, owner);
        internal void HoldTurnPresentation(UnityEngine.Object owner, float seconds)
        { turnPresentationOwner = owner; turnPresentationUntil = Time.unscaledTime + seconds; }
        internal void ReleaseTurnPresentation(UnityEngine.Object owner)
        {
            if (!ReferenceEquals(turnPresentationOwner, owner)) return;
            ClearTurnPresentation();
        }
        private void ClearTurnPresentation() { turnPresentationOwner = null; turnPresentationUntil = 0; }
    }
}
