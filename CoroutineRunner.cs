using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TarkinItemExporter
{
    public class CoroutineRunner : MonoBehaviour
    {
        private static CoroutineRunner _instance;
        public static CoroutineRunner Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<CoroutineRunner>();
                    if (_instance == null)
                    {
                        GameObject runnerObject = new GameObject("CoroutineRunner");
                        _instance = runnerObject.AddComponent<CoroutineRunner>();
                        DontDestroyOnLoad(runnerObject);
                    }
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
}
