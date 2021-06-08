using System;

namespace Capstones.UnityEngineEx
{
    public static class EditorBridge
    {
#pragma warning disable 0067
        private static event Action _OnPlayModeChanged = () => { };
        public static event Action OnPlayModeChanged
        {
            add
            {
#if UNITY_EDITOR
                _OnPlayModeChanged += value;
                RegOnPlayModeChanged();
#endif
            }
            remove
            {
#if UNITY_EDITOR
                _OnPlayModeChanged -= value;
#endif
            }
        }
        private static event Action _PrePlayModeChange = () => { };
        public static event Action PrePlayModeChange
        {
            add
            {
#if UNITY_EDITOR
                _PrePlayModeChange += value;
                RegOnPlayModeChanged();
#endif
            }
            remove
            {
#if UNITY_EDITOR
                _PrePlayModeChange -= value;
#endif
            }
        }
        private static event Action _AfterPlayModeChange = () => { };
        public static event Action AfterPlayModeChange
        {
            add
            {
#if UNITY_EDITOR
                _AfterPlayModeChange += value;
                RegOnPlayModeChanged();
#endif
            }
            remove
            {
#if UNITY_EDITOR
                _AfterPlayModeChange -= value;
#endif
            }
        }
        private static bool _OnPlayModeChangedReged = false;
        private static void RegOnPlayModeChanged()
        {
#if UNITY_EDITOR
            if (!_OnPlayModeChangedReged)
            {
                UnityEditor.EditorApplication.playModeStateChanged += reason =>
                {
                    if (reason == UnityEditor.PlayModeStateChange.ExitingPlayMode || reason == UnityEditor.PlayModeStateChange.ExitingEditMode)
                    {
                        _PrePlayModeChange();
                        _OnPlayModeChanged();
                    }
                    else
                    {
                        _OnPlayModeChanged();
                        _AfterPlayModeChange();
                    }
                };
                _OnPlayModeChangedReged = true;
            }
#endif
        }

        private static event Action _OnDelayedCallOnce = () => { };
        public static event Action OnDelayedCallOnce
        {
            add
            {
#if UNITY_EDITOR
                _OnDelayedCallOnce += value;
                if (UnityEditor.EditorApplication.delayCall == null)
                {
                    UnityEditor.EditorApplication.delayCall = () => { };
                }
                UnityEditor.EditorApplication.delayCall += DoDelayedCallOnce;
#endif
            }
            remove
            {
#if UNITY_EDITOR
                _OnDelayedCallOnce -= value;
#endif
            }
        }
        private static void DoDelayedCallOnce()
        {
            try
            {
                _OnDelayedCallOnce();
            }
            finally
            {
                _OnDelayedCallOnce = () => { };
            }
        }

        private static event Action _OnUpdate = () => { };
        public static event Action OnUpdate
        {
            add
            {
#if UNITY_EDITOR
                _OnUpdate += value;
                if (UnityEditor.EditorApplication.update == null)
                {
                    UnityEditor.EditorApplication.update = () => { };
                }
                UnityEditor.EditorApplication.update += CallUpdate;
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
            }
            remove
            {
#if UNITY_EDITOR
                _OnUpdate -= value;
#endif
            }
        }
        private static event Func<bool> _TerminableUpdate = () => true;
        public static event Func<bool> TerminableUpdate
        {
            add
            {
#if UNITY_EDITOR
                _TerminableUpdate += value;
                if (UnityEditor.EditorApplication.update == null)
                {
                    UnityEditor.EditorApplication.update = () => { };
                }
                UnityEditor.EditorApplication.update += CallUpdate;
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
            }
            remove
            {
#if UNITY_EDITOR
                _TerminableUpdate -= value;
#endif
            }
        }
        private static event Action _WeakUpdate = () => { };
        public static event Action WeakUpdate
        {
            add
            {
#if UNITY_EDITOR
                _WeakUpdate += value;
                if (UnityEditor.EditorApplication.update == null)
                {
                    UnityEditor.EditorApplication.update = () => { };
                }
                UnityEditor.EditorApplication.update += CallUpdate;
#endif
            }
            remove
            {
#if UNITY_EDITOR
                _WeakUpdate -= value;
#endif
            }
        }
#if UNITY_EDITOR
        private static void CallUpdate()
        {
            _WeakUpdate();
            _OnUpdate();
            if (!CallTerminableUpdate())
            {
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                return;
            }
            if (_OnUpdate != null)
            {
                var list = _OnUpdate.GetInvocationList();
                if (list != null && list.Length > 1)
                {
                    UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                    return;
                }
            }
            if (_WeakUpdate != null)
            {
                var list = _WeakUpdate.GetInvocationList();
                if (list != null && list.Length > 1)
                {
                    return;
                }
            }
            if (UnityEditor.EditorApplication.update == CallUpdate)
            {
                UnityEditor.EditorApplication.update = null;
            }
            else if (UnityEditor.EditorApplication.update != null)
            {
                UnityEditor.EditorApplication.update -= CallUpdate;
            }
        }
        private static bool CallTerminableUpdate()
        {
            if (_TerminableUpdate != null)
            {
                var list = _TerminableUpdate.GetInvocationList();
                if (list != null && list.Length > 1)
                {
                    int cnt = list.Length;
                    for (int i = 1; i < list.Length; ++i)
                    {
                        var del = list[i] as Func<bool>;
                        if (del != null)
                        {
                            try
                            {
                                if (!del())
                                {
                                    continue;
                                }
                            }
                            catch
                            {
                                _TerminableUpdate -= del;
                                throw;
                            }
                            _TerminableUpdate -= del;
                        }
                        --cnt;
                    }
                    return cnt <= 1;
                }
                else
                {
                    return true;
                }
            }
            return true;
        }
#endif

        public interface IEditorCoroutine
        {
            bool Done { get; }
            void Stop();
        }
        private class EditorCoroutine : IEditorCoroutine
        {
            public void Stop()
            {
                if (!Done)
                {
                    if (Del != null)
                    {
                        TerminableUpdate -= Del;
                    }
                    if (Work is IDisposable)
                    {
                        try
                        {
                            ((IDisposable)Work).Dispose();
                        }
                        catch (Exception e)
                        {
                            UnityEngine.Debug.LogException(e);
                        }
                    }
                    Done = true;
                }
            }

            public bool Done { get; set; }
            public Func<bool> Del;
            public System.Collections.IEnumerator Work;
        }
        public static IEditorCoroutine StartEditorCoroutine(System.Collections.IEnumerator work)
        {
#if UNITY_EDITOR
            if (work != null)
            {
                EditorCoroutine coroutineInfo = new EditorCoroutine();
                Func<bool> done = () =>
                {
                    try
                    {
                        if (work.MoveNext())
                        {
                            return false;
                        }
                        else
                        {
                            coroutineInfo.Done = true;
                            return true;
                        }
                    }
                    catch
                    {
                        if (work is IDisposable)
                        {
                            try
                            {
                                ((IDisposable)work).Dispose();
                            }
                            catch (Exception e)
                            {
                                UnityEngine.Debug.LogException(e);
                            }
                        }
                        throw;
                    }
                };
                coroutineInfo.Del = done;
                coroutineInfo.Work = work;
                if (!done())
                {
                    TerminableUpdate += done;
                }
                return coroutineInfo;
            }
#else
            if (work != null)
            {
                if (work is IDisposable)
                {
                    try
                    {
                        ((IDisposable)work).Dispose();
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogException(e);
                    }
                }
            }
#endif
            return null;
        }
#pragma warning restore
    }
}