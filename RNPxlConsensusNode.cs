﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Thermo.Magellan.BL.Data;
using Thermo.Magellan.BL.Data.Constants;
using Thermo.Magellan.BL.Processing;
using Thermo.Magellan.BL.Processing.Interfaces;
using Thermo.Magellan.DataLayer.FileIO;
using Thermo.Magellan.EntityDataFramework;
using Thermo.Magellan.Exceptions;
using Thermo.Magellan.MassSpec;
using Thermo.Magellan.Utilities;
using Thermo.Magellan.Proteomics;
using Thermo.Magellan;
using Thermo.PD.EntityDataFramework;
using Thermo.Magellan.Semantics;

namespace PD.OpenMS.AdapterNodes
{
    [ProcessingNode("9A840689-B679-4D0B-8595-9448B1D3EB38",
        DisplayName = "RNPxl Consensus",
        Description = "Post-processes the results of the RNPxl search and connects them with the spectrum view",
        Category = ReportingNodeCategories.Miscellaneous, //TODO
        MainVersion = 1,
        MinorVersion = 49,
        WorkflowType = WorkflowTypeNames.Consensus)]

    [ConnectionPoint(
        "IncomingOpenMSRNPxls",
        ConnectionDirection = ConnectionDirection.Incoming,
        ConnectionMultiplicity = ConnectionMultiplicity.Single,
        ConnectionMode = ConnectionMode.AutomaticToAllPossibleParents,
        ConnectionRequirement = ConnectionRequirement.Optional,
        ConnectedParentNodeConstraint = ConnectedParentConstraint.OnlyToGeneratorsOfRequestedData)]

    [ConnectionPointDataContract(
        "IncomingOpenMSRNPxls",
        ProteomicsDataTypes.Psms)] //TODO

    [ProcessingNodeConstraints(UsageConstraint = UsageConstraint.OnlyOncePerWorkflow)]

    public class RNPxlConsensus : ReportProcessingNode
    {
        public override void OnParentNodeFinished(IProcessingNode sender, ResultsArguments eventArgs)
        {
            // connect RNPxl table and MS/MS spectrum info table
            var rnpxl_items = EntityDataService.CreateEntityItemReader().ReadAll<RNPxlItem>().ToList();

            // store in RT-m/z-dictionary for associating RNPxl table with PD spectra later
            // dictionary RT -> (dictionary m/z -> RNPxlItem.Id)
            // (convert to string, round to 1 decimal)
            var rt_mz_to_rnpxl_id = new Dictionary<string, Dictionary<string, RNPxlItem>>();

            foreach (var r in rnpxl_items)
            {
                string rt_str = String.Format("{0:0.0}", r.rt);
                string mz_str = String.Format("{0:0.0000}", r.orig_mz);
                Dictionary<string, RNPxlItem> mz_dict = null;
                if (rt_mz_to_rnpxl_id.ContainsKey(rt_str))
                {
                    mz_dict = rt_mz_to_rnpxl_id[rt_str];
                }
                else
                {
                    mz_dict = new Dictionary<string, RNPxlItem>();
                }
                mz_dict[mz_str] = r;
                rt_mz_to_rnpxl_id[rt_str] = mz_dict;
            }

            // connect with MS/MS spectrum info table
            EntityDataService.RegisterEntityConnection<RNPxlItem, MSnSpectrumInfo>(ProcessingNodeNumber);
            var rnpxl_to_spectrum_connections = new List<Tuple<RNPxlItem, MSnSpectrumInfo>>();

            var msn_spectrum_info_items = EntityDataService.CreateEntityItemReader().ReadAll<MSnSpectrumInfo>().ToList();
            foreach (var m in msn_spectrum_info_items)
            {
                string rt_str = String.Format("{0:0.0}", m.RetentionTime);
                string mz_str = String.Format("{0:0.0000}", m.MassOverCharge);
                Dictionary<string, RNPxlItem> mz_dict = null;
                if (rt_mz_to_rnpxl_id.ContainsKey(rt_str))
                {
                    mz_dict = rt_mz_to_rnpxl_id[rt_str];
                    if (mz_dict.ContainsKey(mz_str))
                    {
                        RNPxlItem r = mz_dict[mz_str];
                        rnpxl_to_spectrum_connections.Add(Tuple.Create(r, m));
                    }
                }
            }

            EntityDataService.ConnectItems(rnpxl_to_spectrum_connections);
        }
    }
}