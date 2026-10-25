namespace WelpTimeToDie.Player
{
    public enum PlayerMove { None, Move, Dash, Jump, Attack, Block, Wait }

    public readonly struct PlayerMoveDefinition
    {
        public readonly int Duration, Startup, Recharge;
        public PlayerMoveDefinition(int duration, int startup = 0, int recharge = 0)
        {
            Duration = duration;
            Startup = startup;
            Recharge = recharge;
        }
    }

    public static class PlayerMoveCatalog
    {
        public static PlayerMoveDefinition Get(PlayerMove move)
        {
            switch (move)
            {
                case PlayerMove.Move: return new PlayerMoveDefinition(8);
                case PlayerMove.Dash: return new PlayerMoveDefinition(6, recharge: 12);
                case PlayerMove.Jump: return new PlayerMoveDefinition(12, recharge: 16);
                case PlayerMove.Attack: return new PlayerMoveDefinition(9, 4, 12);
                case PlayerMove.Block: return new PlayerMoveDefinition(8, recharge: 4);
                case PlayerMove.Wait: return new PlayerMoveDefinition(4);
                default: return new PlayerMoveDefinition(0);
            }
        }
    }
}
