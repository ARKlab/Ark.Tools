// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 

namespace Ark.Tasks.Messages;

public class ResourceSliceReady
{
    public Resource Resource { get; set; }
    public Slice Slice { get; set; }
}
