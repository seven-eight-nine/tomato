namespace Tomato.EntityHandleSystem;

/// <summary>
/// ref パラメータを受け取るアクションデリゲート。
/// struct エンティティのコールバックで、元の要素を直接変更可能にします。
/// </summary>
public delegate void RefAction<T>(ref T entity);
