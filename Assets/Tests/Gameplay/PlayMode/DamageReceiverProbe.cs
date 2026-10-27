using UnityEngine;
using WelpTimeToDie.Player;

namespace WelpTimeToDie.Gameplay.Tests
{
    public sealed class DamageReceiverProbe : MonoBehaviour, ICombatDamageReceiver
    {
        public int DamageReceived { get; private set; }
        public int HitCount { get; private set; }
        public void ReceiveDamage(int damage, Vector2 sourcePosition) { DamageReceived += damage; HitCount++; }
    }
}
