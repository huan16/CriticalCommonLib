using System;
using System.Collections;
using System.Collections.Generic;
using CriticalCommonLib.Interfaces;
using CriticalCommonLib.Sheets;
using Dalamud.Logging;

namespace CriticalCommonLib.Collections
{
public class ShopCollection : IEnumerable<IShop> {

        #region Constructors

        #region Constructor

        public ShopCollection() {
            _itemLookup = new Dictionary<uint, List<IShop>>();
            _shopLookup = new Dictionary<uint, IShop>();
            CompileLookups();
        }

        #endregion

        #endregion

        public List<IShop> GetShops(uint itemId)
        {
            return _itemLookup.ContainsKey(itemId) ? _itemLookup[itemId] : new List<IShop>();
        }

        public IShop? GetShop(uint shopId)
        {
            //TODO: Add in prehandler lookup
            return _shopLookup.ContainsKey(shopId) ? _shopLookup[shopId] : null;
        }
        
        public static HashSet<uint> ExcludedShops = new HashSet<uint>() {
            1769474, // Currency Test
            1769475, // Materia Test
            1769524, // Items in Development
        };

        private readonly Dictionary<uint, List<IShop>> _itemLookup;
        private readonly Dictionary<uint, IShop> _shopLookup;
        private bool _lookupsCompiled;
        public void CompileLookups()
        {
            if (_lookupsCompiled)
            {
                return;
            }

            _lookupsCompiled = true;
            foreach (var shop in this)
            {
                if(ExcludedShops.Contains(shop.RowId)) continue;
                _shopLookup[shop.RowId] = shop;
                foreach (var itemId in shop.ShopItemIds)
                {
                    if (!_itemLookup.ContainsKey(itemId))
                    {
                        _itemLookup.Add(itemId, new List<IShop>());
                    }
                    _itemLookup[itemId].Add(shop);
                }
            }
        }
        
        public IShop? this[uint key] {
            get { return Get(key); }
        }
        public IShop? Get(uint key) {
            if (_shopLookup.ContainsKey(key))
                return _shopLookup[key];

            return null;
        }

        #region IEnumerable<IShop> Members

        public IEnumerator<IShop> GetEnumerator() {
            return new Enumerator();
        }

        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        #endregion

        #region Enumerator

        private class Enumerator : IEnumerator<IShop> {
            #region Fields

            // ReSharper disable once InconsistentNaming
            private readonly IEnumerator<GCShopEx> _GCShopEnumerator;
            private readonly IEnumerator<GilShopEx> _gilShopEnumerator;
            private readonly IEnumerator<SpecialShopEx> _specialShopEnumerator;
            private readonly IEnumerator<FccShopEx> _fccShopEnumerator;
            private int _state;

            #endregion

            #region Constructors

            #region Constructor

            public Enumerator() {
                _gilShopEnumerator = Service.ExcelCache.GetGilShopExSheet().GetEnumerator();
                _GCShopEnumerator = Service.ExcelCache.GetGCShopExSheet().GetEnumerator();
                _specialShopEnumerator = Service.ExcelCache.GetSpecialShopExSheet().GetEnumerator();
                _fccShopEnumerator = Service.ExcelCache.GetSheet<FccShopEx>().GetEnumerator();
            }

            #endregion

            #endregion

            #region IEnumerator<Item> Members

            public IShop Current { get; private set; } = null!;

            #endregion

            #region IDisposable Members

            private bool _disposed;
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }
        
            private void Dispose(bool disposing)
            {
                if(!_disposed && disposing)
                {
                    _gilShopEnumerator.Dispose();
                    _GCShopEnumerator.Dispose();
                    _specialShopEnumerator.Dispose();
                    _fccShopEnumerator.Dispose();
                }
                _disposed = true;         
            }
            
            ~Enumerator()
            {
#if DEBUG
                // In debug-builds, make sure that a warning is displayed when the Disposable object hasn't been
                // disposed by the programmer.

                if( _disposed == false )
                {
                    Service.Log.Error("There is a disposable object which hasn't been disposed before the finalizer call: " + (this.GetType ().Name));
                }
#endif
                Dispose (true);
            }

            #endregion

            #region IEnumerator Members

            object IEnumerator.Current { get { return Current; } }

            public bool MoveNext() {
                var result = false;

                Current = null!;
                if (_state == 0) {
                    result = _gilShopEnumerator.MoveNext();
                    if (result)
                        Current = _gilShopEnumerator.Current;
                    else
                        ++_state;
                }
                if (_state == 1) {
                    result = _GCShopEnumerator.MoveNext();
                    if (result)
                        Current = _GCShopEnumerator.Current;
                    else
                        ++_state;
                }
                if (_state == 2) {
                    result = _specialShopEnumerator.MoveNext();
                    if (result)
                        Current = _specialShopEnumerator.Current;
                    else
                        ++_state;
                }

                if(_state == 3) {
                    result = _fccShopEnumerator.MoveNext();
                    if (result)
                        Current = _fccShopEnumerator.Current;
                    else
                        ++_state;
                }

                return result;
            }

            public void Reset() {
                _state = 0;
                _gilShopEnumerator.Reset();
                _GCShopEnumerator.Dispose();
                _specialShopEnumerator.Dispose();
                _fccShopEnumerator.Dispose();
            }

            #endregion
        }

        #endregion
    }
}