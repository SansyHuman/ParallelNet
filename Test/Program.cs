using ParallelNet.Common;
using ParallelNet.Lock;

using System.Diagnostics;

ParallelNet.Collection.SortedDictionary<int, string> dict = new ParallelNet.Collection.SortedDictionary<int, string>();
Action<int, string> action = (i, s) => Console.WriteLine($"{i}, {s}");

dict.Add(1, "A");
dict.Inorder(action);
Console.WriteLine();
dict.Add(2, "B");
dict.Inorder(action);
Console.WriteLine();
dict.Add(3, "C");
dict.Inorder(action);
Console.WriteLine();
dict.Add(4, "D");
dict.Inorder(action);
Console.WriteLine();
dict.Add(5, "E");
dict.Inorder(action);
Console.WriteLine();
dict.Add(6, "F");
dict.Inorder(action);
Console.WriteLine();
dict.Add(7, "G");
dict.Inorder(action);
Console.WriteLine();
dict.Add(8, "H");
dict.Inorder(action);
Console.WriteLine();

bool removed = dict.Remove(2);
dict.Inorder(action);
Console.WriteLine();
removed = dict.Remove(1);
dict.Inorder(action);
Console.WriteLine();
removed = dict.Remove(4);
dict.Inorder(action);
Console.WriteLine();
removed = dict.Remove(3);
dict.Inorder(action);
Console.WriteLine();
removed = dict.Remove(6);
dict.Inorder(action);
Console.WriteLine();
removed = dict.Remove(5);
dict.Inorder(action);
Console.WriteLine();
removed = dict.Remove(8);
dict.Inorder(action);
Console.WriteLine();
removed = dict.Remove(7);
dict.Inorder(action);
Console.WriteLine();

ParallelNet.Collection.ArrayList<string> list = new ParallelNet.Collection.ArrayList<string>();

list.Add("H");
list.Add("B");
list.Add("O");
list.Add("T");

list.Clear();

list.Add("H");
list.Add("B");
list.Add("O");
list.Add("T");

foreach (string i in list)
{
    Console.WriteLine(i);
}

int cnt = list.Count;

for (int i = 0; i < cnt; i++)
{
    Console.WriteLine(list[i]);
}

list[0] = "A";
list[2] = "D";

for (int i = 0; i < cnt; i++)
{
    Console.WriteLine(list.PopBack().ResultValue);
}

List<Thread> threads = new List<Thread>();
int workPerThread = 100000;
for (int i = 0; i < Environment.ProcessorCount; i++)
{
    threads.Add(new Thread(() =>
    {
        for (int j = 0; j < workPerThread * 2; j++)
        {
            list.Add("A");
        }
    }));

    threads.Add(new Thread(() =>
    {
        for (int j = 0; j < workPerThread; j++)
        {
            if (list.PopBack().ResultType == Result<string, None>.Type.Failure)
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
int count = list.Count;
int real = 0;

while (list.PopBack().ResultType == Result<string, None>.Type.Success)
    real++;

list.Reduce();

Console.WriteLine(expected == real && expected == count);

Console.WriteLine("Hello world!");