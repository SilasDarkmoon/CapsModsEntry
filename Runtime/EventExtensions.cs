using System;
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
    public class OrderedDelegate<T> where T : class //, Delegate // Delegate constraint is for C# 7.3 only
    {
        public T Handler;
        public int Order;

        public OrderedDelegate(int order, T handler)
        {
            Order = order;
            Handler = handler;
        }
        //public static implicit operator T(OrderedDelegate<T> thiz)
        //{
        //    return thiz.Handler;
        //}
    }
    public class OrderedEvent<T> where T : class //, Delegate // Delegate constraint is for C# 7.3 only
    {
        protected struct HandlerInfo
        {
            public T Handler;
            public int Order;
        }
        protected List<HandlerInfo> _InvocationList = new List<HandlerInfo>();
        private class HandlerInfoComparer : IComparer<HandlerInfo>
        {
            public int Compare(HandlerInfo x, HandlerInfo y)
            {
                return x.Order - y.Order;
            }
        }
        private static HandlerInfoComparer _Comparer = new HandlerInfoComparer();

        public static OrderedEvent<T> operator +(OrderedEvent<T> thiz, T handler)
        {
            thiz.AddHandler(handler);
            return thiz;
        }
        public static OrderedEvent<T> operator -(OrderedEvent<T> thiz, T handler)
        {
            thiz.RemoveHandler(handler);
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
        public void AddHandler(T handler)
        {
            AddHandler(handler, OrderedEventUtils.GetOrder((Delegate)(object)handler));
        }
        public void AddHandler(OrderedDelegate<T> handlerWrapper)
        {
            AddHandler(handlerWrapper.Handler, handlerWrapper.Order);
        }
        public void RemoveHandler(T handler)
        {
            for (int i = 0; i < _InvocationList.Count; ++i)
            {
                if (_InvocationList[i].Handler.Equals(handler))
                {
                    _InvocationList.RemoveAt(i--);
                    _CachedCombined = null;
                }
            }
        }
        public void RemoveHandler(OrderedDelegate<T> handlerWrapper)
        {
            RemoveHandler(handlerWrapper.Handler);
        }
        public void RemoveAll()
        {
            if (_InvocationList.Count > 0)
            {
                _InvocationList.Clear();
                _CachedCombined = null;
            }
        }

        protected T _CachedCombined;
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
        protected virtual void CombineHandlers()
        {
            if (_InvocationList.Count == 0)
            {
                _CachedCombined = null;
            }
            else
            {
                Delegate del = (Delegate)((Delegate)(object)_InvocationList[0].Handler).Clone();
                for (int i = 1; i < _InvocationList.Count; ++i)
                {
                    del = Delegate.Combine(del, (Delegate)(object)_InvocationList[i].Handler);
                }
                _CachedCombined = (T)(object)del;
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

    public static class OrderedEventUtils
    {
        public static int GetOrder(this Delegate handler)
        {
            int order = 0;
            if (handler != null && handler.Method != null)
            {
                var attrs = handler.Method.GetCustomAttributes(typeof(EventOrderAttribute), true);
                if (attrs != null && attrs.Length > 0)
                {
                    order = ((EventOrderAttribute)attrs[0]).Order;
                }
            }
            return order;
        }
    }
}
