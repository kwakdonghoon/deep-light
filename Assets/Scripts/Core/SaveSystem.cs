using System;
using System.IO;
using UnityEngine;

namespace Abyss
{
    /// <summary>진행 저장(JSON). 웹의 localStorage 저장을 Application.persistentDataPath 로 옮긴 것.</summary>
    public static class SaveSystem
    {
        public static string PathOverride;     // 테스트에서 다른 경로를 쓰기 위한 용도
        public static string FilePath { get { return PathOverride ?? Path.Combine(Application.persistentDataPath, "abyss_save.json"); } }

        public static bool Exists() { return File.Exists(FilePath); }

        public static void Save(Sim sim)
        {
            try
            {
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(sim.ToSave()));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);                       // 쓰는 도중 꺼져도 기존 저장이 깨지지 않도록
            }
            catch (Exception e) { Debug.LogWarning("save failed: " + e.Message); }
        }

        public static Sim Load()
        {
            try
            {
                if (!Exists()) return null;
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
                return data == null ? null : Sim.FromSave(data);
            }
            catch (Exception e) { Debug.LogWarning("load failed: " + e.Message); return null; }
        }

        public static void Delete() { try { if (Exists()) File.Delete(FilePath); } catch (Exception) { } }
    }
}
