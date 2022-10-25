using System;
using System.Collections.Generic;

class standart
{
    public bool int_array_equal(int[] Arr1, int[] Arr2)
    {
        if (Arr1 == null || Arr2 == null)
            return false;

        if (Arr1.Length != Arr2.Length)
            return false;

        for (int i = 0; i < Arr1.Length; i++)
            if (Arr1[i] != Arr2[i])
                return false;

        return true;
    }
    public int[] int_array_copy(int[] Arr)
    {
        int[] output = new int[Arr.Length];
        Array.Copy(Arr, output, Arr.Length);

        return output;
    }
    public int[] copy_int_array(int[] input)
    {
        int[] output = new int[input.Length];

        Array.Copy(input, output, input.Length);

        return output;
    }
    public float inverse_sigmoid(float input, float size)
    {
        int sign = input < 0 ? -1 : 1;

        if (Math.Abs(input) == 1)
            return 50;

        return (float)Math.Sqrt(input * input / (1 - input * input)) * size * sign;
    }
    public float sigmoid(float input, float size)
    {
        return (input / size) / (float)Math.Sqrt((input / size) * (input / size) + 1);
    }
    public double sigmoid_derivative(double input, double size)
    {
        return Math.Pow((input / size) * (input / size) + 1, -1.5);
    }
}
