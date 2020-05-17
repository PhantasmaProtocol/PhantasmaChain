﻿using Phantasma.Cryptography;
using Phantasma.Cryptography.ECC;
using Phantasma.Cryptography.Hashing;
using Phantasma.Numerics;
using System;
using System.Linq;
using System.Text;

namespace Phantasma.Ethereum
{
    public static class Sha3Keccack
    {
        public static string CalculateHash(string value)
        {
            var input = Encoding.UTF8.GetBytes(value);
            var output = CalculateHash(input);
            return Base16.Encode(output);
        }

        public static string CalculateHashFromHex(params string[] hexValues)
        {
            var joinedHex = string.Join("", hexValues.Select(x => x.Replace("0x","")).ToArray());
            return Base16.Encode( CalculateHash(Base16.Decode( joinedHex)));
        }

        public static byte[] CalculateHash(byte[] value)
        {
            var digest = new KeccakDigest(256);
            var output = new byte[digest.GetDigestSize()];
            digest.BlockUpdate(value, 0, value.Length);
            digest.DoFinal(output, 0);
            return output;
        }
    }

    public class EthereumKey : IKeyPair
    {
        public byte[] PrivateKey{ get; private set; }
        public byte[] PublicKey { get; private set; }
        public readonly string Address;

        public EthereumKey(byte[] privateKey)
        {
            if (privateKey.Length != 32 && privateKey.Length != 96 && privateKey.Length != 104)
                throw new ArgumentException();
            this.PrivateKey = new byte[32];
            Buffer.BlockCopy(privateKey, privateKey.Length - 32, PrivateKey, 0, 32);

            var pKey = ECCurve.Secp256k1.G * privateKey;

            var bytes = pKey.EncodePoint(true).ToArray();

            this.PublicKey = pKey.EncodePoint(false).Skip(1).ToArray();

            var temp = Base16.Encode(PublicKey);
            var kak = Sha3Keccack.CalculateHash(PublicKey);
            this.Address = "0x"+Base16.Encode( kak.Skip(12).ToArray());
        }

        public static EthereumKey FromWIF(string wif)
        {
            if (wif == null) throw new ArgumentNullException();
            byte[] data = wif.Base58CheckDecode();
            if (data.Length != 34 || data[0] != 0x80 || data[33] != 0x01)
                throw new FormatException();
            byte[] privateKey = new byte[32];
            Buffer.BlockCopy(data, 1, privateKey, 0, privateKey.Length);
            Array.Clear(data, 0, data.Length);
            return new EthereumKey(privateKey);
        }

        private static System.Security.Cryptography.RNGCryptoServiceProvider rnd = new System.Security.Cryptography.RNGCryptoServiceProvider();

        public static EthereumKey Generate()
        {
            var bytes = new byte[32];
            lock (rnd)
            {
                rnd.GetBytes(bytes);
            }
            return new EthereumKey(bytes);
        }

        private string GetWIF()
        {
            byte[] data = new byte[34];
            data[0] = 0x80;
            Buffer.BlockCopy(PrivateKey, 0, data, 1, 32);
            data[33] = 0x01;
            string wif = data.Base58CheckEncode();
            Array.Clear(data, 0, data.Length);
            return wif;
        }

        private static byte[] XOR(byte[] x, byte[] y)
        {
            if (x.Length != y.Length) throw new ArgumentException();
            return x.Zip(y, (a, b) => (byte)(a ^ b)).ToArray();
        }

        public override string ToString()
        {
            return this.Address;
        }

        public Signature Sign(byte[] msg)
        {
            return ECDsaSignature.Generate(this, msg, ECDsaCurve.Secp256r1);
        }
    }
}
