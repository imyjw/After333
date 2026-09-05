using System;
using Project333.Runtime.Domain.Resources;
using UnityEngine;

namespace Project333.Runtime.Infrastructure.Data
{
    [Serializable]
    public struct ResourceSetData
    {
        [SerializeField] private int _mana;
        [SerializeField] private int _qi;
        [SerializeField] private int _power;
        [SerializeField] private int _gold;

        public ResourceSetData(int mana, int qi, int power, int gold)
        {
            _mana = mana;
            _qi = qi;
            _power = power;
            _gold = gold;
        }

        public int Mana => _mana;

        public int Qi => _qi;

        public int Power => _power;

        public int Gold => _gold;

        public ResourceSet ToRuntime()
        {
            return new ResourceSet(_mana, _qi, _power, _gold);
        }
    }
}
