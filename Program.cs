using System;
using SME;
using SME.Components;

namespace sme_sha512
{
    class Program
    {

        // 3 64 csa   : 330 mhz - .012 ns  712 lut 1190 ff 1.532 w
        // 3 64 normal: 330 mhz - .012 ns  650 lut 1190 ff 1.531 w
        // 6 64 csa   : 175 mhz - .344 ns 1336 lut 2138 ff 1.535 w
        // 6 64 normal: 130 mhz - .096 ns 1174 lut 2138 ff 1.531 w
        static void Main(string[] args)
        {
            using (new Simulation())
            {
                var tester = new Tester2(1024);
                var core = new CoreOpt2();
                //var bram = new TrueDualPortMemory<ulong>(80); // TODO 16 for normal, 80 for opt
                //var krom = new SinglePortMemory<ulong>(Constants.k.Length, Constants.k);

                //tester.bramwr = bram.ControlA;
                //tester.bramrd = bram.ReadResultA;
                tester.status = core.status;
                tester.data_in = core.data_out;

                //core.bramwr = bram.ControlB;
                //core.bramrd = bram.ReadResultB;
                core.ctrl = tester.ctrl;
                core.data_in = tester.data_out;
                //core.kromwr = krom.Control;
                //core.kromrd = krom.ReadResult;

                //Simulation.Current.AddTopLevelInputs(tester.bramwr, tester.ctrl);
                //Simulation.Current.AddTopLevelOutputs(tester.bramrd, tester.status);

                Simulation.Current.AddTopLevelInputs(tester.data_out, tester.ctrl);
                Simulation.Current.AddTopLevelOutputs(tester.data_in, tester.status);

                Simulation.Current
                    .BuildCSVFile()
                    .BuildVHDL()
                    .Run();
            }
        }
    }
}
