using ParallelNet.Common;
using ParallelNet.Lock;

using System.Diagnostics;

ParallelNet.Collection.Stack<int> stack = new ParallelNet.Collection.Stack<int>();

stack.Push(1);
stack.Push(2);
stack.Push(3);
stack.Push(4);

stack.Clear();

stack.Push(1);
stack.Push(2);
stack.Push(3);
stack.Push(4);

foreach (int i in stack)
    Console.WriteLine(i);

int cnt = stack.Count;

for (int i = 0; i < cnt; i++)
{
    Console.WriteLine(stack.Pop().ResultValue);
}

List<Thread> threads = new List<Thread>();
int workPerThread = 100000;
for (int i = 0; i < Environment.ProcessorCount; i++)
{
    threads.Add(new Thread(() =>
    {
        for (int j = 0; j < workPerThread * 2; j++)
        {
            stack.Push(1);
        }
    }));

    threads.Add(new Thread(() =>
    {
        for (int j = 0; j < workPerThread; j++)
        {
            if (stack.Pop().ResultType == Result<int, None>.Type.Failure)
            {
                j--;
                continue;
            }
        }
    }));
}

threads.ForEach(t => t.Start());
threads.ForEach(t => t.Join());

int expected = workPerThread * Environment.ProcessorCount;
int count = stack.Count;
int real = 0;

while (stack.Pop().ResultType == Result<int, None>.Type.Success)
    real++;

Console.WriteLine(expected == real && expected == count);

Console.WriteLine("Hello world!");