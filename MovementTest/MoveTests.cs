using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using PanTiltServoMVVM;
using System.Threading.Tasks;
using PanTiltServoMVVM.ServoUtil;

namespace MovementTest
{
    [TestClass]
    public class MoveTests
    {

        /// <summary>
        /// Connect and Disconnect without SN specified
        /// </summary>
        [TestMethod]
        public void ConnectDisconnectDefault()
        {
            try
            {
                var servoController = new PanTiltServoVM();
                servoController.ConnectModule();
                Assert.IsTrue(servoController.IsActive, "Servo did not connect by the return of Connect() func...");
                servoController.Disconnect();
                Assert.IsFalse(servoController.IsActive, "Servo did not connect by the return of Connect() func...");
            }
            catch (Exception ex) { Assert.Fail(ex.Message); }

        }

        /// <summary>
        /// Basic set 50% Pan / Tilt
        /// </summary>
        [TestMethod]
        public void SetPositions()
        {
            try
            {
                var servoController = new PanTiltServoVM();
                servoController.ConnectModule();
                Assert.IsTrue(servoController.IsActive, "Servo did not connect by the return of Connect() func...");

                servoController.PanPercentGoal = 50;
                var startTime = DateTime.Now;
                while (servoController.PanPercentActual != 50) { }
                var totalPanTime = DateTime.Now - startTime;

                servoController.TiltPercentGoal = 50;
                startTime = DateTime.Now;
                while (servoController.TiltPercentActual != 50) { }
                var totalTiltTime = DateTime.Now - startTime;

                Assert.IsTrue(servoController.PanPercentActual == 50, "Pan not at 50% - actually: " + servoController.PanPercentActual + "%");
                Assert.IsTrue(servoController.TiltPercentActual == 50, "Tilt not at 50% - actually: " + servoController.TiltPercentActual +"%");
                Console.WriteLine("Time to Pan: " + totalPanTime.TotalMilliseconds.ToString("0.0") + " ms");
                Console.WriteLine("Time to Tilt: " + totalTiltTime.TotalMilliseconds.ToString("0.0") + " ms");

            }
            catch (Exception ex) { Assert.Fail("caugt ex: " + ex.Message); }
        }

    }
              
}
