import { useCallback, useEffect, useState } from "react";
import { tasksApi, type Task, type TaskStatus } from "../api/tasksApi";
import { appInsights } from "../telemetry/appInsights";

const statusLabels: Record<TaskStatus, string> = {
  todo: "未着手",
  doing: "進行中",
  done: "完了",
};

// 動作確認用の最小構成のタスク一覧(追加・状態変更・削除)
export function TaskList() {
  const [tasks, setTasks] = useState<Task[]>([]);
  const [title, setTitle] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const run = useCallback(async (action: () => Promise<void>) => {
    setError(null);
    try {
      await action();
    } catch (e) {
      console.error(e);
      appInsights?.trackException({ exception: e as Error });
      setError(e instanceof Error ? e.message : String(e));
    }
  }, []);

  const reload = useCallback(
    () =>
      run(async () => {
        setTasks(await tasksApi.list());
      }),
    [run],
  );

  useEffect(() => {
    void reload().finally(() => setLoading(false));
  }, [reload]);

  const handleAdd = (e: React.SyntheticEvent) => {
    e.preventDefault();
    const trimmed = title.trim();
    if (!trimmed) return;
    void run(async () => {
      await tasksApi.create({ title: trimmed });
      setTitle("");
      setTasks(await tasksApi.list());
    });
  };

  const handleStatusChange = (task: Task, status: TaskStatus) =>
    run(async () => {
      await tasksApi.update(task.id, {
        title: task.title,
        status,
        dueDate: task.dueDate,
        memo: task.memo,
      });
      setTasks(await tasksApi.list());
    });

  const handleDelete = (task: Task) =>
    run(async () => {
      await tasksApi.remove(task.id);
      setTasks(await tasksApi.list());
    });

  return (
    <section style={{ textAlign: "left" }}>
      <h2>タスク</h2>
      <form onSubmit={handleAdd}>
        <input
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          placeholder="タスクのタイトル"
          maxLength={200}
        />
        <button type="submit">追加</button>
      </form>
      {error && (
        <pre style={{ color: "crimson", whiteSpace: "pre-wrap" }}>エラー: {error}</pre>
      )}
      {loading ? (
        <p>読み込み中...</p>
      ) : tasks.length === 0 ? (
        <p>タスクはまだありません。</p>
      ) : (
        <ul>
          {tasks.map((task) => (
            <li key={task.id}>
              <span style={{ textDecoration: task.status === "done" ? "line-through" : "none" }}>
                {task.title}
              </span>{" "}
              <select
                value={task.status}
                onChange={(e) => void handleStatusChange(task, e.target.value as TaskStatus)}
              >
                {Object.entries(statusLabels).map(([value, label]) => (
                  <option key={value} value={value}>
                    {label}
                  </option>
                ))}
              </select>{" "}
              <button onClick={() => void handleDelete(task)}>削除</button>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
