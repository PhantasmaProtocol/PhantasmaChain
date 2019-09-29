﻿using Phantasma.Core;
using Phantasma.Cryptography;
using Phantasma.Cryptography.ECC;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Phantasma.Pay.Chains
{
    public class NeoWallet : CryptoWallet
    {
        public const string NeoPlatform = "neo";

        private string neoscanURL;

        public NeoWallet(PhantasmaKeys keys, string neoscanURL) : base(keys)
        {
            if (!neoscanURL.EndsWith("/"))
            {
                neoscanURL += "/";
            }

            this.neoscanURL = neoscanURL;
        }

        public override string Platform => NeoPlatform;

        public override void MakePayment(string symbol, decimal amount, string targetAddress, Action<bool> callback)
        {
            throw new NotImplementedException();
        }

        public override void SyncBalances(Action<bool> callback)
        {
            _balances.Clear();

            var url = $"{neoscanURL}api/main_net/v1/get_balance/{Address}";
            JSONRequest(url, (root) =>
            {
                if (root == null)
                {
                    callback(false);
                    return;
                }

                var temp = GetCryptoCurrencyInfos().Select(x => x.Symbol);
                var symbols = new HashSet<string>(temp);

                root = root.GetNode("balance");
                foreach (var child in root.Children)
                {
                    var symbol = child.GetString("asset_symbol");
                    var amount = child.GetDecimal("amount");
                    if (amount > 0 && symbols.Contains(symbol))
                    {
                        _balances.Add(new WalletBalance(symbol, amount));
                    }
                }

                callback(true);
            });
        }

        public static Address EncodeAddress(string addressText)
        {
            Throw.If(!IsValidAddress(addressText), "invalid neo address");
            var bytes = addressText.Base58CheckDecode();
            return Cryptography.Address.EncodeInterop(NeoPlatform, bytes);
        }

        public static string DecodeAddress(Address address)
        {
            if (!address.IsInterop)
            {
                throw new Exception("not an interop address");
            }

            string platformName;
            byte[] data;
            address.DecodeInterop(out platformName, out data, 21);

            if (platformName != NeoPlatform)
            {
                throw new Exception("not a NEO interop address");
            }

            if (data[0] != 23)
            {
                throw new Exception("invalid NEO address");
            }

            return data.Base58CheckEncode();
        }

        protected override string DeriveAddress(PhantasmaKeys keys)
        {
            ECPoint pKey = ECCurve.Secp256r1.G * keys.PrivateKey;

            var bytes = pKey.EncodePoint(true);

            var script = new byte[bytes.Length + 2];
            script[0] = 0x21;// OpCode.PUSHBYTES33;
            Array.Copy(bytes, 0, script, 1, bytes.Length);
            script[script.Length - 1] = 0xAC; // OpCode.CHECKSIG;

            var scriptHash = script.SHA256().RIPEMD160();

            //this.PublicKey = pKey.EncodePoint(false).Skip(1).ToArray();

            byte[] data = new byte[21];
            data[0] = 23;
            Buffer.BlockCopy(scriptHash.ToArray(), 0, data, 1, 20);
            return data.Base58CheckEncode();
        }

        public override IEnumerable<CryptoCurrencyInfo> GetCryptoCurrencyInfos()
        {
            yield return new CryptoCurrencyInfo("NEO", "NEO", 0, NeoPlatform, CryptoCurrencyCaps.Balance); // TODO check if 1 or 0
            yield return new CryptoCurrencyInfo("GAS", "GAS", 8, NeoPlatform, CryptoCurrencyCaps.Balance);
            yield return new CryptoCurrencyInfo("SOUL", "Phantasma Stake", 8, NeoPlatform, CryptoCurrencyCaps.Balance);
            yield break;
        }

        public static bool IsValidAddress(string text)
        {
            return Phantasma.Neo.Utils.LuxUtils.IsValidAddress(text);
        }

    }
}
