namespace Tomato.HandleSystem;

/// <summary>
/// refパラメータを受け取るアクションデリゲート。
/// 構造体のspawn/despawnコールバックで直接変更を可能にします。
/// </summary>
/// <typeparam name="T">オブジェクトの型</typeparam>
/// <param name="item">オブジェクトへの参照</param>
public delegate void RefAction<T>(ref T item);
