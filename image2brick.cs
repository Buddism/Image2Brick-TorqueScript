//#execOnChange

if($lazyBuildTickMS $= "")
	$lazyBuildTickMS = 100;

if($SCP $= "")
	$SCP = 0;

if($SCP_MaxRead $= "")
	$SCP_MaxRead = 5;

if($i2b_isHost $= "")
	$i2b_isHost = 0;

function makeColorset()
{
	%text = "";
	%numColors = 0;
	for(%i = 0; %i < 64; %i++)
	{
		%color = getColorIDTable(%i);
		if(getWord(%color, 3) < 1)
			continue;

		%colorInt = (mFloor(getWord(%color, 0) * 255) << 16) | (mFloor(getWord(%color, 1) * 255) << 8) | (mFloor(getWord(%color, 2) * 255));
		%text = setRecord(%text, %numColors, %colorInt);
		%numColors++;
	}

	setClipboard(%text);
}

function makeBrickList()
{
	%sc = nametoID(serverConnection);
	%size = %sc.getCount();
	%max = getMin(%size, 10000); //should cover all dbs
	for(%i = 0; %i < %max; %i++)
	{
		%db = %sc.getObject(%i);
		if(%db.getClassName() $= "fxDTSBrickData" && %db.brickSizeZ == 1)
		{
			if(%db.category !$= "Plates" && (%db.category !$= "baseplates" || %db.subCategory !$= "Plain"))
				continue;
			
			if(%db.hasPrint)
				continue;
			
			if(%db.uiName $= "")
				continue;

			if(%db.brickFile !$= "")
				continue;

			if(%list $= "")
				%list = %db.brickSizeX SPC %db.brickSizeY TAB %db.uiName;
			else
				%list = %list NL %db.brickSizeX SPC %db.brickSizeY TAB %db.uiName;
		}
	}

	setClipboard(%list);
}

function lazy_build(%string, %alt)
{
	if($SCP)
		commandToServer('SCPC');

	%numColors = 0;
	for(%i = 0; %i < 64; %i++)
	{
		%color = getColorIDTable(%i);
		if(getWord(%color, 3) < 1)
			continue;
		
		$lazybuild_pal[%numColors] = %i;
		%numColors++;
	}

	deleteVariables("$lazybuild_bricks*");
	%numLinesRead = 0;
	%currLineRead = 0;
	while(true)
	{
		%line = getRecord(%string, %currLineRead);
		%currLineRead++;

		//is it a uiname record?
		if(strlen(%line) == 0)
			break;

		if(%rip++ > 500)
		{
			error("BAD UINAMETABLE TRANSLATOR");
			return;
		}

		$lazybuild_bricks[firstWord(%line)] = $uiNameTable[trim(restWords(%line))];
		%numLinesRead++;
	}

	//trim off the uiname table
	%string = getRecords(%string, %numLinesRead + 1);
	//setclipboard(%string);

	if($SCP $= "")
		$SCP = true;

	$lazybuild_length = getRecordCount(%string);
	$lazybuild_string = %string;

	$lazybuild_lastPos = "0 0 0";
	$lazybuild_lastBrick = -1;
	$lazybuild_lastColor = -1;

	if($lazyBuildTickMS $= "")
		$lazyBuildTickMS = 1;

	deleteVariables("$lazybuild_queue*");
	$lazybuild_queuecount = 0;

	scp_reset_stack();

	//split the large record string into individual 1000 record slices (performance reasons!)
	lazy_build_split();
	lazy_build_schedule();

	//if(!$SCP)
		//imgBrick_buildTick();
}

function scp_push(%data)
{
	if($i2b_isHost)
		return serverCmdSCP2(clientGroup.getObject(0), getWord(%data, 0), getWord(%data, 1), getWord(%data, 2), getWord(%data, 3), getWord(%data, 4), getWord(%data, 5));

	$scp_stack[$scp_stack_count + 0] = %data;
	$scp_stack_count++;

	if($scp_stack_count >= 3)
		scp_pop_all();
}

function scp_pop_all()
{
	if($scp_stack_count != 3)
	{
		warn("scp_stack_count != 3 (", $scp_stack_count, ")");
		if($scp_stack_count > 3)
		{
			scp_reset_stack();
			error("STACK TOO LARGE");
		}
		for(%i = 0; %i < $scp_stack_count; %i++)
		{
			%data = $scp_stack[%i];
			commandToServer('SCP2', getWord(%data, 0), getWord(%data, 1), getWord(%data, 2), getWord(%data, 3), getWord(%data, 4), getWord(%data, 5));
		}

		scp_reset_stack();
		return;
	}
	
	%data0 = $scp_stack[0];
	%data1 = $scp_stack[1];
	%data2 = $scp_stack[2];

	if(strlen(%data0 @ %data1 @ %data2) > 256*3)
	{
		error("DATA TOO LARGE");
		return;
	}

	commandToServer('TripleSCP', %data0, %data1, %data2);

	scp_reset_stack();
}

function scp_reset_stack()
{
	//deleteVariables("$scp_stack*");
	//$scp_stack[0] = "";
	//$scp_stack[1] = "";
	//$scp_stack[2] = "";
	$scp_stack_count = 0;
}

//most of this is stolen from buildbot
function imgBuild_brick(%x, %y, %colorID, %brickID, %rotation)
{
	%x = %x / 2;
	%y = %y / 2;

	%newPos = buildbot_getshiftpos(%x SPC %y SPC 0, %brickID);

	%prevpos = $lazybuild_lastPos;

	%shiftx = getWord(%newPos, 0) - getWord(%prevpos, 0);
	%shifty = getWord(%newPos, 1) - getWord(%prevpos, 1);

	if($lazybuild_lastBrick != %brickID)
	{
		commandToServer('instantUseBrick', %brickID);
		$lazybuild_lastBrick = %brickID;
	}

	commandToServer('shiftBrick', %shiftX, %shiftY, 0);

	if($lazybuild_lastColor != %colorID)
	{
		commandToServer('useSprayCan', %colorID);
		$lazybuild_lastColor = %colorID;
	}

	//if(%brickID.brickSizeX > %brickID.brickSizeY)
		//%rotation = !%rotation;

	if(%rotation)
		commandToServer('rotateBrick', 1);

	commandToServer('plantbrick');

	if(%rotation)
		commandToServer('rotateBrick', -1);

	$lazybuild_lastPos = %newPos;
}

function imgBrick_buildTick()
{
	%index = $lazybuild_queueIndex + 0;
	if(%index >= $lazybuild_queuecount)
	{
		newChatHud_AddLine("real fin");
			
		return;
	}

	%x = $lazybuild_queue[%index, x];
	%y = $lazybuild_queue[%index, y];
	%colorID = $lazybuild_queue[%index, color];
	%brickID = $lazybuild_queue[%index, brickID];
	%rotation = $lazybuild_queue[%index, rotation];

	%volume = %brickID.brickSizeX * %brickID.brickSizeY;

	imgBuild_brick(%x, %y, %colorID, %brickID, %rotation);

	if($showProgress)
		clientcmdBottomprint((%index / $lazybuild_queuecount) * 100, 1, 1);

	$lazybuild_queueIndex++;
	$lazybuild_queue = schedule($lazyBuildTickMS, serverConnection, imgBrick_buildTick);
}

function imgBuild_queue(%x, %y, %colorID, %brickID, %rotation)
{
	%index = $lazybuild_queuecount;
	$lazybuild_queue[%index, x] = %x;
	$lazybuild_queue[%index, y] = %y;

	$lazybuild_queue[%index, color] = %colorID;
	$lazybuild_queue[%index, brickID] = %brickID;
	$lazybuild_queue[%index, rotation] = %rotation;

	$lazybuild_queuecount++;
}

function lazy_build_split()
{
	%string = $lazybuild_string;
	%length = getRecordCount(%string);

	for(%i = 0; %i < mCeil(%length / 1000); %i++)
	{
		$lazybuild_string[%i] = getRecords(%string, %i * 1000, (%i + 1) * 1000 - 1);
		$lazybuild_length[%i] = getRecordCount($lazybuild_string[%i]);
	}

	$lazybuild_splitCount = mCeil(%length / 1000);
	echo("split the string of "@ %length @" records into "@ $lazybuild_splitCount @" slices");
}

function lazy_build_schedule(%currSplit, %currIndex)
{
	if(%currSplit >= $lazybuild_splitCount)
	{
		newchathud_addline("LAZY BUILD FINISH");

		if($SCP)
			scp_pop_all();
		else
			imgBrick_buildTick();

		return;
	}

	//make sure its an int
	%currSplit = %currSplit | 0;
	%string = $lazybuild_string[%currSplit];
	%length = $lazybuild_length[%currSplit];
	%maxRead = ($SCP ? $SCP_MaxRead : 200);
	%startI = %currIndex;
	%endI = getMin(%currIndex + %maxRead, %length);

	if($showProgress)
		clientcmdBottomprint((%currSplit / $lazybuild_splitCount) * 100, 1, 1);

	for(%currIndex = %startI; %currIndex < %endI; %currIndex++)
	{
		if(%currIndex >= %length)
		{
			%currSplit++;
			%currIndex = 0;

			break;
		}

		%record = getRecord(%string, %currIndex);

		%x = getWord(%record, 0);
		%y = getWord(%record, 1);
		%colorID = $lazybuild_pal[getWord(%record, 2)];
		if(%colorID $= "")
		{
			error("INVALID COLOR ID");
			return;
		}

		%rotation = getWord(%record, 3);
			
		%uiNameID = getWords(%record, 4, 99);

		%brickID = $lazybuild_bricks[%uinameID];
		if(%brickID $= "" || %uiNameID $= "")
		{
			newChatHud_AddLine("INVALID UINAME " @ %uiNameID SPC %brickID);
			return;
		}

		if($SCP)
			scp_push(%x SPC %y SPC 0 SPC %colorID SPC %brickID SPC %rotation);
		else {
			imgBuild_queue(%x, %y, %colorID, %brickID, %rotation);
		}
	}

	if(%currIndex >= %length)
	{
		%currSplit++;
		%currIndex = 0;
	}


	$lazybuildschedule = schedule(1, serverConnection.getControlObject(), lazy_build_schedule, %currSplit, %currIndex);
}