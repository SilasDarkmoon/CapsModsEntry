﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Capstones.UnityEngineEx
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class EventOrderAttribute : Attribute
    {
        public int Order;
        public EventOrderAttribute(int order)
        {
            Order = order;
        }
    }
    public class OrderedEvent<T> where T : Delegate
    {
        private struct HandlerInfo
        {
            public T Handler;
            public int Order;
        }
        private List<HandlerInfo> _InvocationList = new List<HandlerInfo>();
        private class HandlerInfoComparer : IComparer<HandlerInfo>
        {
            public int Compare(HandlerInfo x, HandlerInfo y)
            {
                return x.Order - y.Order;
            }
        }
        private static HandlerInfoComparer _Comparer = new HandlerInfoComparer();

        public static OrderedEvent<T> operator+(OrderedEvent<T> thiz, T handler)
        {
            int order = 0;
            if (handler.Method != null)
            {
                var attrs = handler.Method.GetCustomAttributes(typeof(EventOrderAttribute), true);
                if (attrs != null && attrs.Length > 0)
                {
                    order = ((EventOrderAttribute)attrs[0]).Order;
                }
            }
            thiz.AddHandler(handler, order);
            return thiz;
        }
        public static OrderedEvent<T> operator-(OrderedEvent<T> thiz, T handler)
        {
            for (int i = 0; i < thiz._InvocationList.Count; ++i)
            {
                if (thiz._InvocationList[i].Handler.Equals(handler))
                {
                    thiz._InvocationList.RemoveAt(i--);
                    thiz._CachedCombined = null;
                }
            }
            return thiz;
        }

        public void AddHandler(T handler, int order)
        {
            var index = _InvocationList.BinarySearch(new HandlerInfo() { Order = order }, _Comparer);
            if (index >= 0)
            {
                for (index = index + 1; index < _InvocationList.Count && _InvocationList[index].Order == order; ++index)
                {
                }
                _InvocationList.Insert(index, new HandlerInfo() { Order = order, Handler = handler });
            }
            else
            {
                _InvocationList.Insert(~index, new HandlerInfo() { Order = order, Handler = handler });
            }
            _CachedCombined = null;
        }

        private T _CachedCombined;
        public T Handler
        {
            get
            {
                if (_CachedCombined == null)
                {
                    CombineHandlers();
                }
                return _CachedCombined;
            }
        }
        private void CombineHandlers()
        {
            if (_InvocationList.Count == 0)
            {
                _CachedCombined = null;
            }
            else
            {
                Delegate del = (T)_InvocationList[0].Handler.Clone();
                for (int i = 1; i < _InvocationList.Count; ++i)
                {
                    del = Delegate.Combine(del, _InvocationList[i].Handler);
                }
                _CachedCombined = (T)del;
            }
        }

        public static implicit operator T(OrderedEvent<T> thiz)
        {
            if (thiz == null)
            {
                return null;
            }
            else
            {
                return thiz.Handler;
            }
        }
        public static implicit operator OrderedEvent<T>(T handler)
        {
            OrderedEvent<T> rv = new OrderedEvent<T>();
            rv += handler;
            return rv;
        }
    }
}
