using System;
using SME;
using SME.Components;

namespace sme_sha512
{
    class Program
    {
        static void Main(string[] args)
        {
            using (new Simulation())
            {
                var tester = new Tester(1024);
                var core = new Core();
                var bram = new TrueDualPortMemory<ulong>(16);

                tester.bramwr = bram.ControlA;
                tester.bramrd = bram.ReadResultA;
                tester.status = core.status;

                core.bramwr = bram.ControlB;
                core.bramrd = bram.ReadResultB;
                core.ctrl = tester.ctrl;

                Simulation.Current.AddTopLevelInputs(tester.bramwr, tester.ctrl);
                Simulation.Current.AddTopLevelOutputs(tester.bramrd, tester.status);

                Simulation.Current
                    .BuildCSVFile()
                    .BuildVHDL()
                    .Run();
            }
        }
    }
}
