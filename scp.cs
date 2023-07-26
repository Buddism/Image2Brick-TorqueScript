function SCP_angleIDFromPlayer(%player, %data)
{
	%playerAngle = getAngleIDFromPlayer (%player);

	if ((%playerAngle + %data.orientationFix) % 4 == 0)
		return 0;

	if ((%playerAngle + %data.orientationFix) % 4 == 1)
		return 3;

	if ((%playerAngle + %data.orientationFix) % 4 == 2)
		return 2;

	if ((%playerAngle + %data.orientationFix) % 4 == 3)
		return 1;
}
function SCP_getShiftVector(%forward, %right, %x, %y, %z)
{
	return vectorAdd(vectorAdd(vectorScale(%forward, %x / 2), vectorScale(%right, -%y / 2)), "0 0" SPC %z * 0.2);
}

function SCP_roundVectorToWhole(%vector)
{
	return mFloor(getWord(%vector, 0) + 0.5) SPC mFloor(getWord(%vector, 1) + 0.5) SPC mFloor(getWord(%vector, 2) + 0.5);
}
function serverCmdSCPC(%client)
{
	if(!%client.isSuperAdmin && !%client.canSCP)
		return;

	%player = %client.player;
	%tempbrick = %player.tempbrick;
	if(!isObject(%player) || !isObject(%tempbrick))
		return;

	%player.SCP_Forward = SCP_roundVectorToWhole(%player.getForwardVector());
	%player.SCP_Right = SCP_roundVectorToWhole(vectorCross(%player.SCP_Forward, %player.getUpVector()));
	%player.SCP_faceAngleID = SCP_angleIDFromPlayer(%player, %tempbrick);
	%player.SCP_origin = %tempbrick.getPosition();
}
$SCP_AngleID[0] = "1 0 0 0";
$SCP_AngleID[1] = "0 0 1 1.5708";
$SCP_AngleID[2] = "0 0 1 3.14159";
$SCP_AngleID[3] = "0 0 -1 1.5708";

function serverCmdTripleSCP(%client, %data0, %data1, %data2)
{
	if(!%client.isSuperAdmin && !%client.canSCP)
		return;

	serverCmdSCP2(%client, getWord(%data0, 0), getWord(%data0, 1), getWord(%data0, 2), getWord(%data0, 3), getWord(%data0, 4), getWord(%data0, 5));
	serverCmdSCP2(%client, getWord(%data1, 0), getWord(%data1, 1), getWord(%data1, 2), getWord(%data1, 3), getWord(%data1, 4), getWord(%data1, 5));
	serverCmdSCP2(%client, getWord(%data2, 0), getWord(%data2, 1), getWord(%data2, 2), getWord(%data2, 3), getWord(%data2, 4), getWord(%data2, 5));
}

function serverCmdSCP2(%client, %x, %y, %z, %colorID, %brickID, %rotFaceAway)
{
	if(!%client.isSuperAdmin && !%client.canSCP)
		return;

	%player = %client.player;
	%tempbrick = %player.tempbrick;
	if(!isObject(%player) || !isObject(%tempbrick))
		return;

	%origin = %tempbrick.getTransform();
	if(vectorDist(%player.SCP_origin, %origin) > 0.1) //if the client moved their tempbrick, create new center
		serverCmdSCPC(%client);

	%originalDB = %tempbrick.getDataBlock();
	if(isObject(%brickID) && %brickID.getClassName() $= "fxDTSBrickData")
	{
		//turn the origin into the closest edge of the brick to the player
		%bnx = %originalDB.brickSizeX / -2;
		%bny = %originalDB.brickSizeY / -2;
		%origin = vectorAdd(%origin, SCP_getShiftVector(%player.SCP_Forward, %player.SCP_Right, %bnx, %bny, 0));
		%tempbrick.setDatablock(%brickID);
	}

	%angleID = %player.SCP_faceAngleID;
	if(%rotFaceAway)
		%angleID = (%angleID + %rotFaceAway) % 4;

	%forwardAdjustedShift = SCP_getShiftVector(%player.SCP_Forward, %player.SCP_Right, %x, %y, %z);
	%tempbrick.setTransform(vectorAdd(%origin, %forwardAdjustedShift) SPC $SCP_AngleID[%angleID]);

	%tempbrick.setColor(%colorID % 64);
	serverCmdPlantBrick(%client);

	%tempbrick.setDatablock(%originalDB);
	%tempbrick.setTransform(%player.SCP_origin);
}