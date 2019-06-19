using System;
using System.Linq;
using SME;
using SME.Components;
using System.Security.Cryptography;
using System.Text;

namespace sme_sha512
{

    // TODO unopt
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

    // TODO bruger BRAM
    [ClockedProcess]
    public class CoreOpt : SimpleProcess
    {
        [InputBus]
        public TrueDualPortMemory<ulong>.IReadResultB bramrd;
        [InputBus]
        public Control ctrl;
        [InputBus]
        public SinglePortMemory<ulong>.IReadResult kromrd;

        [OutputBus]
        public TrueDualPortMemory<ulong>.IControlB bramwr;
        [OutputBus]
        public SinglePortMemory<ulong>.IControl kromwr;
        [OutputBus]
        public Status status = Scope.CreateBus<Status>();

        ulong a, b, c, d, e, f, g, h;
        int i;
        ulong[] hs = new ulong[8];

        enum States { idle, init, load, prep, pre_update0, pre_update1, update, post, flush };
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

        // TODO VHDL cannot find static variables...
        ulong tmp;
        ulong[] hs_reset = {
            0x6a09e667f3bcc908, 0xbb67ae8584caa73b, 0x3c6ef372fe94f82b, 0xa54ff53a5f1d36f1,
            0x510e527fade682d1, 0x9b05688c2b3e6c1f, 0x1f83d9abfb41bd6b, 0x5be0cd19137e2179 };
        ulong[] w = new ulong[16];

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
                    for (int j = 0; j < hs.Length; j++)
                        hs[j] = hs_reset[j];
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

                    if (i > 1)
                        w[i-2] = bramrd.Data;
                    if (i == 17)
                    {
                        i = 0;
                        state = States.pre_update0;
                    }
                    else
                        i++;
                    break;
                case States.pre_update0:
                    kromwr.Enabled = true;
                    kromwr.Address = i;
                    state = States.pre_update1;
                    i++;
                    a = hs[0];
                    b = hs[1];
                    c = hs[2];
                    d = hs[3];
                    e = hs[4];
                    f = hs[5];
                    g = hs[6];
                    h = hs[7];
                    break;
                case States.pre_update1:
                    kromwr.Address = i;
                    state = States.update;
                    i++;
                    break;
                    //(h + (r(e,14) ^ r(e,18) ^ r(e,41)) + (g ^ (e & (f ^ g))) + k + w) + ((r(a,28) ^ r(a,34) ^ r(a,39)) + ((a & b) | (c & (a | b))))
                case States.update:
                    bramwr.Enabled = i < 80;
                    bramwr.Address = i;
                    kromwr.Enabled = i < 80;
                    kromwr.Address = i;
                    ulong temp1 = h + S3(e) + F1(e, f, g) + kromrd.Data + w[0];
                    ulong temp2 = S2(a) + F0(a, b, c);
                    
                    h = g;
                    g = f;
                    f = e;
                    e = d + temp1;
                    d = c;
                    c = b;
                    b = a;
                    a = temp1 + temp2;

                    // TODO 
                    // Unopt : 50 mhz og 80 cycler load og 80 cycler compute = (50/160)*1024 = 320 mbit
                    // Performance for sidste commit: 74 mhz og (64*5 + 2) cycler for load og 80 cycler compute = (74/402)*1024 = 188.498 mbit
                    // Performance for denne: 82 mhz og 16 + 2 cycler for load og 80 cycler compute = (82/98)*1024 = 856.816 mbit
                    // Opt2: 98 mhz og 16 + 2 for load og 80 cycler compute = (98/98)*1024 = 1024 mbit

                    tmp = S1(w[14]) + w[9] + S0(w[1]) + w[0];
                    for (int j = 0; j < 15; j++)
                        w[j] = w[j+1];
                    w[15] = tmp;

                    if (i < 81)
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

    // TODO bruger Carry Save Adder
    [ClockedProcess]
    public class CoreOpt2 : SimpleProcess
    {
        [InputBus]
        public Control ctrl;
        [InputBus]
        public Data data_in;

        [OutputBus]
        public Status status = Scope.CreateBus<Status>();
        [OutputBus]
        public Data data_out = Scope.CreateBus<Data>();

        ulong a, b, c, d, e, f, g, h;
        int i;
        ulong[] hs = new ulong[8];
        ulong[] hs_reset = Constants.hs;
        ulong[] k = Constants.k;
        ulong[] w = new ulong[16];

        enum States { idle, init, load, update, post, flush };
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

        private ulong CSA4(ulong a, ulong b, ulong c, ulong d)
        {
            ulong tmp1s = a ^ b ^ c;
            ulong tmp1c = (((a ^ b) & c) | (a & b)) << 1;

            ulong tmp2s = tmp1s ^ tmp1c ^ d;
            ulong tmp2c = (((tmp1s ^ tmp1c) & d) | (tmp1s & tmp1c)) << 1;

            return tmp2s + tmp2c;
        }

        private ulong CSA5(ulong a, ulong b, ulong c, ulong d, ulong e)
        {
            ulong tmp1s = a ^ b ^ c;
            ulong tmp1c = (((a ^ b) & c) | (a & b)) << 1;

            ulong tmp2s = tmp1s ^ tmp1c ^ d;
            ulong tmp2c = (((tmp1s ^ tmp1c) & d) | (tmp1s & tmp1c)) << 1;

            ulong tmp3s = tmp2s ^ tmp2c ^ e;
            ulong tmp3c = (((tmp2s ^ tmp2c) & e) | (tmp2s & tmp2c)) << 1;

            return tmp3s + tmp3c;
        }

        protected override void OnTick()
        {
            switch (state)
            {
                case States.idle:
                    data_out.valid = false;
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
                    for (int j = 0; j < hs.Length; j++)
                        hs[j] = hs_reset[j];
                    state = States.idle;
                    status.busy = false;
                    break;
                case States.load:
                    a = hs[0];
                    b = hs[1];
                    c = hs[2];
                    d = hs[3];
                    e = hs[4];
                    f = hs[5];
                    g = hs[6];
                    h = hs[7];
                    i = 0;
                    state = States.update;
                    break;
                case States.update:
                    if (i >= 16 || data_in.valid)
                    {
                        //ulong tmp = S1(w[14]) + w[9] + S0(w[1]) + w[0];
                        //
                        ulong tmp01s = S1(w[14]) ^ w[9] ^ S0(w[1]);
                        ulong tmp01c = (((S1(w[14]) ^ w[9]) & S0(w[1])) | (S1(w[14]) & w[9])) << 1;

                        ulong tmp02s = tmp01s ^ tmp01c ^ w[0];
                        ulong tmp02c = (((tmp01s ^ tmp01c) & w[0]) | (tmp01s & tmp01c)) << 1;

                        ulong tmp0 = tmp02s + tmp02c;
                        //
                        for (int j = 0; j < 15; j++)
                            w[j] = w[j+1];
                        if (i < 16)
                            w[15] = data_in.val;
                        else
                            w[15] = tmp0;

                        //ulong temp1 = h + S3(e) + F1(e, f, g) + k[i] + w[15];
                        //
                        ulong tmp11s = h ^ S3(e) ^ F1(e, f, g);
                        ulong tmp11c = (((h ^ S3(e)) & F1(e, f, g)) | (h & S3(e))) << 1;

                        ulong tmp12s = tmp11s ^ tmp11c ^ k[i];
                        ulong tmp12c = (((tmp11s ^ tmp11c) & k[i]) | (tmp11s & tmp11c)) << 1;

                        ulong tmp13s = tmp12s ^ tmp12c ^ w[15];
                        ulong tmp13c = (((tmp12s ^ tmp12c) & w[15]) | (tmp12s & tmp12c)) << 1;
                        //
                        //ulong temp2 = S2(a) + F0(a, b, c);
                        //
                        ulong tmpa1s = tmp13s ^ tmp13c ^ S2(a);
                        ulong tmpa1c = (((tmp13s ^ tmp13c) & S2(a)) | (tmp13s & tmp13c)) << 1;

                        ulong tmpa2s = tmpa1s ^ tmpa1c ^ F0(a, b, c);
                        ulong tmpa2c = (((tmpa1s ^ tmpa1c) & F0(a, b, c)) | (tmpa1s & tmpa1c)) << 1;
                        //
                        
                        h = g;
                        g = f;
                        f = e;
                        //e = d + temp1;
                        //
                        ulong tmpe1s = tmp13s ^ tmp13c ^ d;
                        ulong tmpe1c = (((tmp13s ^ tmp13c) & d) | (tmp13s & tmp13c)) << 1;
                        e = tmpe1s + tmpe1c;
                        //
                        d = c;
                        c = b;
                        b = a;
                        //a = temp1 + temp2;
                        //
                        a = tmpa2s + tmpa2c;
                        //

                        if (i < 79)
                            i++;
                        else
                            state = States.post;
                    }
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
                    data_out.valid = true;
                    data_out.val = hs[i];
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

    public class Tester2 : SimulationProcess
    {
        [InputBus]
        public Data data_in;
        [InputBus]
        public Status status;

        [OutputBus]
        public Control ctrl = Scope.CreateBus<Control>();
        [OutputBus]
        public Data data_out = Scope.CreateBus<Data>();

        public Tester2(int size_bytes)
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

                // Update the hashing
                data_out.valid = false;
                ctrl.update = true;
                await ClockAsync();
                ctrl.update = false;
                await ClockAsync();

                // Move the data
                for (int i = 0; i < block.Length; i++)
                {
                    data_out.valid = true;
                    data_out.val = block[i];
                    await ClockAsync();
                }
                data_out.valid = false;

                while (status.busy)
                    await ClockAsync();
            }
            Console.WriteLine("\r{0}/{0} blocks... Done", num_blocks);

            // Move the computed hash to block ram
            ctrl.finish = true;
            await ClockAsync();
            ctrl.finish = false;
            await ClockAsync();

            // Get the result
            byte[] result = new byte[8*8];
            for (int i = 0; i < 8; i++)
            {
                if (!data_in.valid)
                    i--;
                else
                {
                    byte[] tmp = unpack_bytes(data_in.val);
                    for (int j = 0; j < 8; j++)
                        result[i*8 + j] = tmp[j];
                }
                await ClockAsync();
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