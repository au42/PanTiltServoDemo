using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PanTiltServoMVVM.ServoUtil
{
    public class ServoEventArgs : EventArgs
    {
        public MovementType MoveType { get; private set; }
        public ushort? Position { get; private set; }
        public int? PosPercent { get; private set; }

        public ServoEventArgs(ushort posValue, MovementType type = MovementType.UNKNOWN)
        {
            Position = posValue;
        }

        public ServoEventArgs(int percentValue, MovementType type = MovementType.UNKNOWN)
        {
            PosPercent = percentValue;
        }
    }
}
