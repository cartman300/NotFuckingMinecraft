﻿using OpenTK;
using System;
using System.Collections;
using System.Linq;

namespace NFM {

	[Serializable]
	public class OctreeNode<T> : IOctree<T> where T : IEquatable<T> {
		public const float NoMinSize = -1;
		public const float DefaultMinSize = 5;
		protected internal ArrayList Items;
		protected internal OctreeNode<T>[] Branch;
		protected internal int MaxItems;
		protected internal float MinSize;
		public OctreeBox Bounds;
		protected internal bool AllTheSamePoint;
		protected internal float FirstX;
		protected internal float FirstY;
		protected internal float FirstZ;

		public OctreeNode(float xMax, float xMin, float yMax, float yMin, float zMax, float zMin, int maximumItems)
			: this(xMax, xMin, yMax, yMin, zMax, zMin, maximumItems, NoMinSize) {
		}

		public OctreeNode(float xMax, float xMin, float yMax, float yMin, float zMax, float zMin, int maximumItems, float minimumSize) {
			Bounds = new OctreeBox(xMax, xMin, yMax, yMin, zMax, zMin);
			MaxItems = maximumItems;
			MinSize = minimumSize;
			Items = ArrayList.Synchronized(new ArrayList(10));
		}

		public bool HasChildren() {
			return Branch != null;
		}

		protected internal void Split() {
			// Make sure we're bigger than the minimum, if we care,
			if (MinSize != NoMinSize)
				if (Math.Abs(Bounds.Top - Bounds.Bottom) < MinSize &&
				Math.Abs(Bounds.Right - Bounds.Left) < MinSize &&
				Math.Abs(Bounds.Front - Bounds.Back) < MinSize)
					return;
			var nsHalf = (float)(Bounds.Top - (Bounds.Top - Bounds.Bottom) * 0.5);
			var ewHalf = (float)(Bounds.Right - (Bounds.Right - Bounds.Left) * 0.5);
			var fbHalf = (float)(Bounds.Front - (Bounds.Front - Bounds.Back) * 0.5);
			Branch = new OctreeNode<T>[8];
			Branch[0] = new OctreeNode<T>(ewHalf, Bounds.Left, Bounds.Front, fbHalf, Bounds.Top, nsHalf, MaxItems); //left-front-top
			Branch[1] = new OctreeNode<T>(Bounds.Right, ewHalf, Bounds.Front, fbHalf, Bounds.Top, nsHalf, MaxItems);
			Branch[2] = new OctreeNode<T>(ewHalf, Bounds.Left, Bounds.Front, fbHalf, nsHalf, Bounds.Bottom, MaxItems);
			Branch[3] = new OctreeNode<T>(Bounds.Right, ewHalf, Bounds.Front, fbHalf, nsHalf, Bounds.Bottom, MaxItems);
			Branch[4] = new OctreeNode<T>(ewHalf, Bounds.Left, fbHalf, Bounds.Back, Bounds.Top, nsHalf, MaxItems); //left-back-top
			Branch[5] = new OctreeNode<T>(Bounds.Right, ewHalf, fbHalf, Bounds.Back, Bounds.Top, nsHalf, MaxItems);
			Branch[6] = new OctreeNode<T>(ewHalf, Bounds.Left, fbHalf, Bounds.Back, nsHalf, Bounds.Bottom, MaxItems);
			Branch[7] = new OctreeNode<T>(Bounds.Right, ewHalf, fbHalf, Bounds.Back, nsHalf, Bounds.Bottom, MaxItems);
			var temp = (ArrayList)Items.Clone();
			Items.Clear();
			var things = temp.GetEnumerator();
			while (things.MoveNext()) {
				AddNode((OctreeLeaf<T>)things.Current);
			}
		}

		protected internal OctreeNode<T> GetChild(float x, float y, float z) {
			return Bounds.PointWithinBounds(x, y, z)
			? (Branch != null
			? (from t in Branch
			   where t.Bounds.PointWithinBounds(x, y, z)
			   select t.GetChild(x, y, z)).
			FirstOrDefault()
			: this)
			: null;
		}

		#region Add Node
		public bool AddNode(float x, float y, float z, T obj) {
			return AddNode(new OctreeLeaf<T>(x, y, z, obj));
		}

		public bool AddNode(Vector3 vector, T obj) {
			return AddNode(new OctreeLeaf<T>(vector.X, vector.Y, vector.Z, obj));
		}

		public bool AddNode(double x, double y, double z, T obj) {
			return AddNode(new OctreeLeaf<T>(x, y, z, obj));
		}

		public bool AddNode(OctreeLeaf<T> leaf) {
			if (Branch == null) {
				Items.Add(leaf);
				if (Items.Count == 1) {
					AllTheSamePoint = true;
					FirstX = leaf.X;
					FirstY = leaf.Y;
					FirstZ = leaf.Z;
				} else {
					if (FirstX != leaf.X || FirstY != leaf.Y || FirstZ != leaf.Z) {
						AllTheSamePoint = false;
					}
				}
				if (Items.Count > MaxItems && !AllTheSamePoint)
					Split();
				return true;
			}
			OctreeNode<T> node = GetChild(leaf.X, leaf.Y, leaf.Z);
			return node != null && node.AddNode(leaf);
		}
		#endregion

		#region Remove Node
		public T RemoveNode(float x, float y, float z, T obj) {
			return RemoveNode(new OctreeLeaf<T>(x, y, z, obj));
		}

		public T RemoveNode(Vector3 vector, T obj) {
			return RemoveNode(new OctreeLeaf<T>(vector.X, vector.Y, vector.Z, obj));
		}

		public T RemoveNode(double x, double y, double z, T obj) {
			return RemoveNode(new OctreeLeaf<T>(x, y, z, obj));
		}

		public T RemoveNode(OctreeLeaf<T> leaf) {
			if (Branch == null) {
				// This must be the node that has it...
				for (int i = 0; i < Items.Count; i++) {
					var qtl = (OctreeLeaf<T>)Items[i];
					if (!leaf.LeafObject.Equals(qtl.LeafObject))
						continue;
					Items.RemoveAt(i);
					return qtl.LeafObject;
				}
			} else {
				OctreeNode<T> node = GetChild(leaf.X, leaf.Y, leaf.Z);
				if (node != null) {
					return node.RemoveNode(leaf);
				}
			}
			return default(T);
		}
		#endregion

		#region Get Node
		public T GetNode(float x, float y, float z) {
			return GetNode(x, y, z, Double.PositiveInfinity);
		}

		public T GetNode(Vector3 vector) {
			return GetNode(vector.X, vector.Y, vector.Z, Double.PositiveInfinity);
		}

		public T GetNode(float x, float y, float z, double shortestDistance) {
			T closest = default(T);
			if (Branch == null) {
				// var childDistance = this.Bounds.BorderDistance(x, y, z);
				//if (childDistance > shortestDistance)
				// return null;
				foreach (OctreeLeaf<T> leaf in Items) {
					var distance = Math.Sqrt(
					Math.Pow(x - leaf.X, 2.0) +
					Math.Pow(y - leaf.Y, 2.0) +
					Math.Pow(z - leaf.Z, 2.0));
					if (!(distance < shortestDistance))
						continue;
					shortestDistance = distance;
					closest = leaf.LeafObject;
				}
				return closest;
			}
			// Check the distance of the bounds of the branch,
			// versus the bestDistance. If there is a boundary that
			// is closer, then it is possible that another node has an
			// object that is closer.
			foreach (OctreeNode<T> t in Branch) {
				var childDistance = t.Bounds.BorderDistance(x, y, z);
				if (!(childDistance < shortestDistance))
					continue;
				T test = t.GetNode(x, y, z, shortestDistance);
				if (test != null)
					closest = test;
			}
			return closest;
		}

		public T GetNode(Vector3 vector, double shortestDistance) {
			return GetNode(vector.X, vector.Y, vector.Z, shortestDistance);
		}

		public ArrayList GetNode(float xMax, float xMin, float yMax, float yMin, float zMax, float zMin) {
			return GetNode(new OctreeBox(xMax, xMin, yMax, yMin, zMax, zMin), ArrayList.Synchronized(new ArrayList(10)));
		}

		public ArrayList GetNode(float xMax, float xMin, float yMax, float yMin, float zMax, float zMin, ArrayList nodes) {
			return GetNode(new OctreeBox(xMax, xMin, yMax, yMin, zMax, zMin), nodes);
		}

		public ArrayList GetNode(OctreeBox rect, ArrayList nodes) {
			if (Branch == null) {
				var things = Items.GetEnumerator();
				while (things.MoveNext()) {
					var qtl = (OctreeLeaf<T>)things.Current;
					if (qtl != null && rect.PointWithinBounds(qtl.X, qtl.Y, qtl.Z))
						nodes.Add(qtl.LeafObject);
				}
			} else {
				foreach (var t in Branch.Where(t => t.Bounds.Within(rect))) {
					t.GetNode(rect, nodes);
				}
			}
			return nodes;
		}
		#endregion
		#region Get Nodes
		public ArrayList GetNodes(float x, float y, float z, double radius) {
			var nodes = new ArrayList();
			if (Branch == null) {
				foreach (OctreeLeaf<T> leaf in Items) {
					double distance = Math.Sqrt(
					Math.Pow(x - leaf.X, 2.0) +
					Math.Pow(y - leaf.Y, 2.0) +
					Math.Pow(z - leaf.Z, 2.0));
					if (distance < radius)
						nodes.Add(leaf.LeafObject);
				}
				return nodes;
			}
			foreach (object test in from t in Branch
									let childDistance = t.Bounds.BorderDistance(x, y, z)
									where childDistance < radius
									select t.GetNode(x, y, z, radius) into test
									where test != null
									select test) {
				nodes.Add(test);
			}
			return nodes;
		}

		public ArrayList GetNodes(Vector3 vector, double radius) {
			return GetNodes(vector.X, vector.Y, vector.Z, radius);
		}

		public ArrayList GetNodes(float x, float y, float z, double minRadius, double maxRadius) {
			var nodes = new ArrayList();
			if (Branch == null) {
				foreach (var leaf in from OctreeLeaf<T> leaf in Items
									 let distance = Math.Sqrt(
										Math.Pow(x - leaf.X, 2.0) +
										Math.Pow(y - leaf.Y, 2.0) +
										Math.Pow(z - leaf.Z, 2.0))
									 where distance >= minRadius && distance < maxRadius
									 select leaf) {
					nodes.Add(leaf.LeafObject);
				}
				return nodes;
			}
			// Check the distance of the bounds of the branch,
			// versus the bestDistance. If there is a boundary that
			// is closer, then it is possible that another node has an
			// object that is closer.
			foreach (object test in from t in Branch
									let childDistance = t.Bounds.BorderDistance(x, y, z)
									where childDistance > minRadius && childDistance <= maxRadius
									select t.GetNode(x, y, z, minRadius) into test
									where test != null
									select test) {
				nodes.Add(test);
			}
			return nodes;
		}

		public ArrayList GetNodes(Vector3 vector, double minRadius, double maxRadius) {
			return GetNodes(vector.X, vector.Y, vector.Z, minRadius, maxRadius);
		}
		#endregion
		public void Clear() {
			Items.Clear();
			if (Branch == null)
				return;
			foreach (var t in Branch) {
				t.Clear();
			}
			Branch = null;
		}
	}

}