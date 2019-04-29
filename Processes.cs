using System;
using System.Linq;
using SME;
using SME.Components;
using System.Security.Cryptography;
using System.Text;

namespace sme_sha512
{

    [InitializedBus]
    public interface Control : IBus
    {
        bool init { get; set; }
        bool update { get; set; }
        bool finish { get; set; }
    }

    [InitializedBus]
    public interface Status : IBus
    {
        bool busy { get; set; }
    }

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

        int i;
        ulong[] hs = new ulong[8]; //0, h1, h2, h3, h4, h5, h6, h7;
        ulong a, b, c, d, e, f, g, h;

        ulong[] k = { 
            0x428a2f98d728ae22, 0x7137449123ef65cd, 0xb5c0fbcfec4d3b2f, 0xe9b5dba58189dbbc, 0x3956c25bf348b538, 
            0x59f111f1b605d019, 0x923f82a4af194f9b, 0xab1c5ed5da6d8118, 0xd807aa98a3030242, 0x12835b0145706fbe, 
            0x243185be4ee4b28c, 0x550c7dc3d5ffb4e2, 0x72be5d74f27b896f, 0x80deb1fe3b1696b1, 0x9bdc06a725c71235, 
            0xc19bf174cf692694, 0xe49b69c19ef14ad2, 0xefbe4786384f25e3, 0x0fc19dc68b8cd5b5, 0x240ca1cc77ac9c65, 
            0x2de92c6f592b0275, 0x4a7484aa6ea6e483, 0x5cb0a9dcbd41fbd4, 0x76f988da831153b5, 0x983e5152ee66dfab, 
            0xa831c66d2db43210, 0xb00327c898fb213f, 0xbf597fc7beef0ee4, 0xc6e00bf33da88fc2, 0xd5a79147930aa725, 
            0x06ca6351e003826f, 0x142929670a0e6e70, 0x27b70a8546d22ffc, 0x2e1b21385c26c926, 0x4d2c6dfc5ac42aed, 
            0x53380d139d95b3df, 0x650a73548baf63de, 0x766a0abb3c77b2a8, 0x81c2c92e47edaee6, 0x92722c851482353b, 
            0xa2bfe8a14cf10364, 0xa81a664bbc423001, 0xc24b8b70d0f89791, 0xc76c51a30654be30, 0xd192e819d6ef5218, 
            0xd69906245565a910, 0xf40e35855771202a, 0x106aa07032bbd1b8, 0x19a4c116b8d2d0c8, 0x1e376c085141ab53, 
            0x2748774cdf8eeb99, 0x34b0bcb5e19b48a8, 0x391c0cb3c5c95a63, 0x4ed8aa4ae3418acb, 0x5b9cca4f7763e373, 
            0x682e6ff3d6b2b8a3, 0x748f82ee5defb2fc, 0x78a5636f43172f60, 0x84c87814a1f0ab72, 0x8cc702081a6439ec, 
            0x90befffa23631e28, 0xa4506cebde82bde9, 0xbef9a3f7b2c67915, 0xc67178f2e372532b, 0xca273eceea26619c, 
            0xd186b8c721c0c207, 0xeada7dd6cde0eb1e, 0xf57d4f7fee6ed178, 0x06f067aa72176fba, 0x0a637dc5a2c898a6, 
            0x113f9804bef90dae, 0x1b710b35131c471b, 0x28db77f523047d84, 0x32caab7b40c72493, 0x3c9ebe0a15c9bebc, 
            0x431d67c49c100d4c, 0x4cc5d4becb3e42b6, 0x597f299cfc657e2a, 0x5fcb6fab3ad6faec, 0x6c44198c4a475817 };
        
        ulong[] w = new ulong[80];

        // Pre-processing (Padding):
        // begin with the original message of length L bits
        // append a single '1' bit
        // append K '0' bits, where K is the minimum number >= 0 such that L + 1 + K + 64 is a multiple of 512
        // append L as a 64-bit big-endian integer, making the total post-processed length a multiple of 512 bits

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
                    hs[0] = 0x6a09e667f3bcc908;
                    hs[1] = 0xbb67ae8584caa73b;
                    hs[2] = 0x3c6ef372fe94f82b;
                    hs[3] = 0xa54ff53a5f1d36f1;
                    hs[4] = 0x510e527fade682d1;
                    hs[5] = 0x9b05688c2b3e6c1f;
                    hs[6] = 0x1f83d9abfb41bd6b;
                    hs[7] = 0x5be0cd19137e2179;
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
                    ulong temp1 = h + S3(e) + F1(e, f, g) + k[i] + w[i];
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
        public TrueDualPortMemory<ulong>.IControlA bramwr;
        [OutputBus]
        public Control ctrl = Scope.CreateBus<Control>();

        private ulong pack_bytes(byte[] data)
        {
            ulong tmp = 0;
            for (int i = 0; i < 8; i++)
                tmp |= (ulong)data[i] << (64-((i+1) << 3));
            return tmp;
        }

        private byte[] unpack_bytes(ulong data)
        {
            byte[] tmp = new byte[16];
            for (int i = 0; i < tmp.Length; i++)
                tmp[i] = (byte)(data >> ((7-i) << 3));
            return tmp;
        }

        private void print_hash(byte[] digest)
        {
            for (int i = 0; i < digest.Length; i++)
                Console.Write("{0:x2}", digest[i]);
            Console.WriteLine();
        }

        SHA512 sha = new SHA512Managed();

        public override async System.Threading.Tasks.Task Run()
        {
            await ClockAsync();

            // TODO fix at input skal være i 8 fold
            // TODO fix at køre flere blokke
            // TODO fix så den kører på random data istedet

            // Pack the data
            ulong[] block = new ulong[16];
            byte[] data = Encoding.ASCII.GetBytes("aoeuaoeuaoeuaoeu");
            for (int i = 0; i < data.Length; i += 8)
                block[i >> 3] = pack_bytes(data.Skip(i).Take(8).ToArray());
            int bytes = data.Length;
            int word_i = bytes / 8;
            int byte_i = bytes % 8;
            block[word_i] |= (ulong)0x80 << ((7-byte_i) << 3);
            ulong bits = (ulong)bytes << 3;
            block[15] = bits;

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

            // Init the hashing
            ctrl.init = true;
            await ClockAsync();
            ctrl.init = false;
            await ClockAsync();
            while (status.busy)
                await ClockAsync();
            
            // Update the hashing
            ctrl.update = true;
            await ClockAsync();
            ctrl.update = false;
            await ClockAsync();
            while (status.busy)
                await ClockAsync();

            // Get the hash value
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