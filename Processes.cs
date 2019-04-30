using System;
using System.Linq;
using SME;
using SME.Components;
using System.Security.Cryptography;
using System.Text;

namespace sme_sha512
{

    [ClockedProcess]
    public class Core : SimpleProcess
    {
        [InputBus]
        public TrueDualPortMemory<ulong>.IReadResultB bramrd;
        [InputBus]
        public Control ctrl;

        [OutputBus]
        public TrueDualPortMemory<ulong>.IControlB bramwr;
        [OutputBus]
        public Status status = Scope.CreateBus<Status>();

        ulong a, b, c, d, e, f, g, h;
        int i;
        ulong[] hs = new ulong[8];
        ulong[] w = new ulong[80];

        enum States { idle, init, load, prep, update, post, flush };
        States state = States.idle;

        private ulong rightrotate(ulong val, int amount)
        {
            return (val >> amount) | (val << (64 - amount));
        }

        private ulong S0(ulong val)
        {
            return rightrotate(val, 1) ^ rightrotate(val, 8) ^ (val >> 7);
        }

        private ulong S1(ulong val)
        {
            return rightrotate(val, 19) ^ rightrotate(val, 61) ^ (val >> 6);
        }

        private ulong S2(ulong val)
        {
            return rightrotate(val, 28) ^ rightrotate(val, 34) ^ rightrotate(val, 39);
        }
        
        private ulong S3(ulong val)
        {
            return rightrotate(val, 14) ^ rightrotate(val, 18) ^ rightrotate(val, 41);
        }

        private ulong F0(ulong x, ulong y, ulong z)
        {
            return (x & y) | (z & (x | y));
        }

        private ulong F1(ulong x, ulong y, ulong z)
        {
            return z ^ (x & (y ^ z));
        }

        protected override void OnTick()
        {
            switch (state)
            {
                case States.idle:
                    bramwr.Enabled = false;
                    bramwr.IsWriting = false;
                    if (ctrl.init)
                    {
                        status.busy = true;
                        state = States.init;
                    }
                    else if (ctrl.update)
                    {
                        status.busy = true;
                        state = States.load;
                        i = 0;
                    }
                    else if (ctrl.finish)
                    {
                        status.busy = true;
                        state = States.flush;
                        i = 0;
                    }
                    break;
                case States.init:
                    for (int j = 0; j < Constants.hs.Length; j++)
                        hs[j] = Constants.hs[j];
                    state = States.idle;
                    status.busy = false;
                    break;
                case States.load:
                    if (i < 16)
                    {
                        bramwr.Enabled = true;
                        bramwr.Address = i;
                    }
                    else
                        bramwr.Enabled = false;

                    if (i > 1 && i <= 17)
                        w[i-2] = bramrd.Data;

                    if (i >= 16)
                        w[i] = S1(w[i-2]) + w[i-7] + S0(w[i-15]) + w[i - 16];
                    
                    if (i == 79)
                    {
                        i = 0;
                        state = States.update;
                        a = hs[0];
                        b = hs[1];
                        c = hs[2];
                        d = hs[3];
                        e = hs[4];
                        f = hs[5];
                        g = hs[6];
                        h = hs[7];
                    }
                    else
                        i++;
                    break;
                case States.update:
                    ulong temp1 = h + S3(e) + F1(e, f, g) + Constants.k[i] + w[i];
                    ulong temp2 = S2(a) + F0(a, b, c);
                    
                    h = g;
                    g = f;
                    f = e;
                    e = d + temp1;
                    d = c;
                    c = b;
                    b = a;
                    a = temp1 + temp2;
                    if (i < 79)
                        i++;
                    else
                        state = States.post;
                    break;
                case States.post:
                    hs[0] += a;
                    hs[1] += b;
                    hs[2] += c;
                    hs[3] += d;
                    hs[4] += e;
                    hs[5] += f;
                    hs[6] += g;
                    hs[7] += h;
                    state = States.idle;
                    status.busy = false;
                    break;
                case States.flush:
                    bramwr.Enabled = true;
                    bramwr.Address = i;
                    bramwr.IsWriting = true;
                    bramwr.Data = hs[i];
                    if (i == 7)
                    {
                        state = States.idle;
                        status.busy = false;
                    }
                    else
                        i++;
                    break;
            }
        }
    }

    public class Tester : SimulationProcess
    {
        [InputBus]
        public Status status;
        [InputBus]
        public TrueDualPortMemory<ulong>.IReadResultA bramrd;

        [OutputBus]
        public Control ctrl = Scope.CreateBus<Control>();
        [OutputBus]
        public TrueDualPortMemory<ulong>.IControlA bramwr;

        public Tester(int size_bytes)
        {
            this.size = size_bytes;
        }

        private ulong pack_bytes(byte[] data)
        {
            ulong tmp = 0;
            for (int i = 0; i < Math.Min(data.Length, 8); i++)
                tmp |= (ulong)data[i] << (64-((i+1) << 3));
            return tmp;
        }

        private void print_hash(byte[] digest)
        {
            for (int i = 0; i < digest.Length; i++)
                Console.Write("{0:x2}", digest[i]);
            Console.WriteLine();
        }

        private byte[] unpack_bytes(ulong data)
        {
            byte[] tmp = new byte[16];
            for (int i = 0; i < tmp.Length; i++)
                tmp[i] = (byte)(data >> ((7-i) << 3));
            return tmp;
        }

        Random rng = new Random();
        SHA512 sha = new SHA512Managed();
        int size;

        public override async System.Threading.Tasks.Task Run()
        {
            await ClockAsync();

            // Init the hashing
            ctrl.init = true;
            await ClockAsync();
            ctrl.init = false;
            await ClockAsync();
            while (status.busy)
                await ClockAsync();

            // Generate the data
            byte[] data = new byte[size];
            for (int i = 0; i < data.Length; i++)
                data[i] = (byte) rng.Next();

            // Process the data in blocks
            int num_blocks = ((size+17)/Constants.block_size_bytes);
            if (((size+17)%Constants.block_size_bytes) != 0)
                num_blocks++;
            for (int j = 0; j < num_blocks; j++)
            {
                Console.Write("\r{0}/{1} blocks...", j, num_blocks);

                // Fill the block
                ulong[] block = new ulong[Constants.block_size_bytes/8];
                for (int i = 0; i < block.Length; i++)
                    block[i] = pack_bytes(data.Skip((j*Constants.block_size_bytes)+(i*8)).Take(8).ToArray());
                
                // Check if last of data, in which case put stop bit
                if (data.Length >= j*Constants.block_size_bytes && data.Length < (j+1)*Constants.block_size_bytes)
                {
                    int bytes = data.Length - (j*Constants.block_size_bytes);
                    int word_i = bytes / 8;
                    int byte_i = bytes % 8;
                    block[word_i] |= Constants.stop_bit << ((7-byte_i) * 8);
                }

                // Check if last block, in which case put size of data in bits
                if (j == num_blocks-1)
                {
                    ulong bits = (ulong)data.Length * 8;
                    block[14] = 0;
                    block[15] = bits; // Assumes data will not be bigger than 2^64 bits (i.e. 16 exabits)
                }

                // Move to block ram
                for (int i = 0; i < block.Length; i++)
                {
                    bramwr.Enabled = true;
                    bramwr.Address = i;
                    bramwr.IsWriting = true;
                    bramwr.Data = block[i];
                    await ClockAsync();
                }
                bramwr.Enabled = false;
                bramwr.IsWriting = false;
                
                // Update the hashing
                ctrl.update = true;
                await ClockAsync();
                ctrl.update = false;
                await ClockAsync();
                while (status.busy)
                    await ClockAsync();
            }
            Console.WriteLine("\r{0}/{0} blocks... Done", num_blocks);

            // Move the computed hash to block ram
            ctrl.finish = true;
            await ClockAsync();
            ctrl.finish = false;
            await ClockAsync();
            while (status.busy)
                await ClockAsync();

            // Get the result
            byte[] result = new byte[8*8];
            for (int i = 0; i < 8; i++)
            {
                bramwr.Enabled = true;
                bramwr.Address = i;
                await ClockAsync();
                await ClockAsync();
                byte[] tmp = unpack_bytes(bramrd.Data);
                for (int j = 0; j < 8; j++)
                    result[i*8 + j] = tmp[j];
            }

            // Compare with library results
            byte[] verified = sha.ComputeHash(data);

            bool all_eq = true;
            for (int i = 0; i < result.Length; i++)
                all_eq = all_eq && (result[i] == verified[i]);
            if (!all_eq)
            {
                Console.WriteLine("Error.");
                print_hash(result);
                print_hash(verified);   
            }
        }
    }

}