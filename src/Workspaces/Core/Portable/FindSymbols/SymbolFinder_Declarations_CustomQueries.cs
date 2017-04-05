﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Internal.Log;

namespace Microsoft.CodeAnalysis.FindSymbols
{
    // Logic related to finding declarations with a completely custom predicate goes here.
    // Completely custom predicates can not be optimized in any way as there is no way to
    // tell what the predicate will return true for.

    public static partial class SymbolFinder
    {
        /// <summary>
        /// Find the symbols for declarations made in source with a matching name.
        /// </summary>
        public static Task<IEnumerable<ISymbol>> FindSourceDeclarationsAsync(Solution solution, Func<string, bool> predicate, CancellationToken cancellationToken = default(CancellationToken))
            => FindSourceDeclarationsAsync(solution, predicate, SymbolFilter.All, cancellationToken);

        /// <summary>
        /// Find the symbols for declarations made in source with a matching name.
        /// </summary>
        public static async Task<IEnumerable<ISymbol>> FindSourceDeclarationsAsync(Solution solution, Func<string, bool> predicate, SymbolFilter filter, CancellationToken cancellationToken = default(CancellationToken))
            => await FindSourceDeclarationsWithCustomQueryAsync(
                solution, SearchQuery.CreateCustom(predicate), filter, cancellationToken).ConfigureAwait(false);

        private static async Task<ImmutableArray<ISymbol>> FindSourceDeclarationsWithCustomQueryAsync(
            Solution solution, SearchQuery query, SymbolFilter filter, CancellationToken cancellationToken)
        {
            if (solution == null)
            {
                throw new ArgumentNullException(nameof(solution));
            }

            if (query.Name != null && string.IsNullOrWhiteSpace(query.Name))
            {
                return ImmutableArray<ISymbol>.Empty;
            }

            using (Logger.LogBlock(FunctionId.SymbolFinder_Solution_Predicate_FindSourceDeclarationsAsync, cancellationToken))
            {
                var result = ArrayBuilder<ISymbol>.GetInstance();
                foreach (var projectId in solution.ProjectIds)
                {
                    var project = solution.GetProject(projectId);
                    var symbols = await FindSourceDeclarationsWithCustomQueryAsync(project, query, filter, cancellationToken).ConfigureAwait(false);
                    result.AddRange(symbols);
                }

                return result.ToImmutableAndFree();
            }
        }

        /// <summary>
        /// Find the symbols for declarations made in source with a matching name.
        /// </summary>
        public static Task<IEnumerable<ISymbol>> FindSourceDeclarationsAsync(Project project, Func<string, bool> predicate, CancellationToken cancellationToken = default(CancellationToken))
            => FindSourceDeclarationsAsync(project, predicate, SymbolFilter.All, cancellationToken);

        /// <summary>
        /// Find the symbols for declarations made in source with a matching name.
        /// </summary>
        public static async Task<IEnumerable<ISymbol>> FindSourceDeclarationsAsync(Project project, Func<string, bool> predicate, SymbolFilter filter, CancellationToken cancellationToken = default(CancellationToken))
            => await FindSourceDeclarationsWithCustomQueryAsync(
                project, SearchQuery.CreateCustom(predicate), filter, cancellationToken).ConfigureAwait(false);

        private static async Task<ImmutableArray<ISymbol>> FindSourceDeclarationsWithCustomQueryAsync(
            Project project, SearchQuery query, SymbolFilter filter, CancellationToken cancellationToken)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            if (query.Name != null && string.IsNullOrWhiteSpace(query.Name))
            {
                return ImmutableArray<ISymbol>.Empty;
            }

            using (Logger.LogBlock(FunctionId.SymbolFinder_Project_Predicate_FindSourceDeclarationsAsync, cancellationToken))
            {
                if (!await project.ContainsSymbolsWithNameAsync(query.GetPredicate(), filter, cancellationToken).ConfigureAwait(false))
                {
                    return ImmutableArray<ISymbol>.Empty;
                }

                var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);

                var unfiltered = compilation.GetSymbolsWithName(query.GetPredicate(), filter, cancellationToken).ToImmutableArray();
                return FilterByCriteria(unfiltered, filter);
            }
        }
    }
}