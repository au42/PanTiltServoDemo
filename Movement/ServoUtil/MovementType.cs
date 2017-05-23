using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PanTiltServoMVVM.ServoUtil
{
    public enum MovementType
    {
        UNKNOWN = 0,
        STOPPED,
        PAN,
        TILT,
        BOTH
    }
}
