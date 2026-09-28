using System.Collections.Generic;

namespace EchoZone.Enemy
{
    /// <summary>Unity·네트워크 없이 유휴 객체 보관과 중복 반환만 관리합니다.</summary>
    public sealed class ReusePoolBrick<T> where T : class
    {
        /// <summary>마지막 반환 객체부터 재사용할 보관함입니다.</summary>
        private readonly Stack<T> available = new();
        /// <summary>동일 객체가 두 번 보관되지 않도록 검사합니다.</summary>
        private readonly HashSet<T> stored = new();
        /// <summary>현재 보관 중인 객체 수입니다.</summary>
        public int Count => available.Count;
        /// <summary>이미 반환된 객체인지 확인합니다.</summary>
        public bool Contains(T item) => stored.Contains(item);
        /// <summary>보관 한도 내에서 한 번만 반환합니다.</summary>
        public bool TryReturn(T item, int capacity)
        {
            if (item == null || stored.Contains(item) || available.Count >= capacity) return false;
            stored.Add(item);
            available.Push(item);
            return true;
        }
        /// <summary>보관 객체 하나를 꺼냅니다. 생성은 외부 Glue의 책임입니다.</summary>
        public bool TryTake(out T item)
        {
            if (available.Count == 0) { item = null; return false; }
            item = available.Pop();
            stored.Remove(item);
            return true;
        }
    }
}
