using System;
using System.Collections.Generic;
using Android.Views;
using Android.Widget;

namespace Emzi0767.AndroidBot
{
    public class LogItemAdapter : BaseAdapter<string>
    {
        public override int Count { get { return this.Items.Count; } }
        private List<string> Items { get; set; }
        private LayoutInflater Inflater { get; set; }

        public LogItemAdapter(List<string> items, LayoutInflater inflater)
        {
            this.Items = items;
            this.Inflater = inflater;
        }

        public void Add(string item)
        {
            this.Items.Add(item);
        }

        public override long GetItemId(int position)
        {
            return position;
        }

        public override View GetView(int position, View convertView, ViewGroup parent)
        {
            var lv = this.Inflater.Inflate(Resource.Layout.LogItem, null);
            var tv = (TextView)lv.FindViewById(Resource.Id.logitem_text);
            tv.Text = this[position];

            return lv;
        }

        public override string this[int position]
        {
            get { return this.Items[position]; }
        }
    }
}